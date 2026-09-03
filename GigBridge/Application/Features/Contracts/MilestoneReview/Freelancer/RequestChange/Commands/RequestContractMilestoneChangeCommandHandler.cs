using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Contracts.Interfaces;
using Application.Common.InternalServices.Contracts.Models;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.Models.Email;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Application.Common.InternalServices.ESign.Services;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.RequestChange.Commands;

public sealed class RequestContractMilestoneChangeCommandHandler :
    IRequestHandler<RequestContractMilestoneChangeCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IContractPlanChangeEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RequestContractMilestoneChangeCommandHandler> _logger;

    public RequestContractMilestoneChangeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notificationService,
        IEmailService emailService,
        IContractPlanChangeEmailRenderer emailRenderer,
        IConfiguration configuration,
        ILogger<RequestContractMilestoneChangeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
        _emailService = emailService;
        _emailRenderer = emailRenderer;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ContractWorkflowResponse> Handle(
        RequestContractMilestoneChangeCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(contract => contract.ContractsId == command.ContractId, cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.PendingSignature)
        {
            throw new BadRequestException("Milestone changes can only be requested before escrow funding.");
        }

        var freelancerProfile = await ContractParticipantGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        var now = _dateTimeService.UtcNow;
        contract.Status = (int)ContractStatus.PendingContractDetails;
        contract.UpdatedAt = now;

        var documents = await _context.Set<EsignDocument>()
            .Where(document =>
                document.ContractsId == contract.ContractsId &&
                document.Status != (int)ESignDocumentStatus.Voided &&
                document.Status != (int)ESignDocumentStatus.Expired)
            .ToListAsync(cancellationToken);

        var documentIds = documents
            .Select(document => document.EsignDocumentsId)
            .ToList();

        if (documentIds.Any())
        {
            var signatures = await _context.Set<EsignSignature>()
                .Where(signature => documentIds.Contains(signature.EsignDocumentsId))
                .ToListAsync(cancellationToken);

            foreach (var signature in signatures)
            {
                signature.Status = (int)ESignSignatureStatus.Declined;
                signature.DeclinedAt = now;
                signature.DeclineReason = "Voided due to milestone change request.";
            }
        }

        foreach (var document in documents)
        {
            document.Status = (int)ESignDocumentStatus.Voided;
            document.FinalizedAt = null;
            document.FinalizedDocumentFileName = null;
            document.FinalizedDocumentSizeBytes = null;
            document.PdfDocumentHash = null;
            document.PdfSignatureCount = 0;
            document.PdfDocumentSizeBytes = null;
            await ESignArtifactStorage.DeleteAsync(
                _context,
                document.EsignDocumentsId,
                ESignArtifactType.FinalizedDocx,
                cancellationToken);
            await ESignArtifactStorage.DeleteAsync(
                _context,
                document.EsignDocumentsId,
                ESignArtifactType.Pdf,
                cancellationToken);
            ESignDocumentRevision.Advance(document, now);
            await ESignDocumentRevision.EnqueueAsync(
                _context,
                document,
                now,
                cancellationToken);
        }

        var reason = command.Request.Reason?.Trim();

        var changeReason = string.IsNullOrWhiteSpace(reason)
            ? "The freelancer asked for the milestones to be reworked before signing again."
            : reason;

        await ContractPlanChangeRequests.RecordAsync(
            _context,
            contract.ContractsId,
            command.UserId,
            changeReason,
            command.Request.AffectedMilestoneIds,
            command.Request.AffectedWorkItemIds,
            ContractPlanChangeOrigin.MilestoneReview,
            now,
            cancellationToken);

        var message = string.IsNullOrWhiteSpace(reason)
            ? "Milestone changes requested. Client must update contract details and both parties must sign again."
            : $"Milestone changes requested: {reason}";

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            message,
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId != Guid.Empty)
        {
            var notificationContent = string.IsNullOrWhiteSpace(reason)
                ? $"The freelancer requested milestone changes for \"{contract.Title}\". Please update the contract milestones."
                : $"The freelancer requested milestone changes for \"{contract.Title}\": {reason}";

            await _notificationService.CreateNotificationAsync(
                clientUserId,
                NotificationType.MilestoneUpdated,
                "Milestone change requested",
                notificationContent,
                contract.ContractsId,
                "Contract",
                cancellationToken);

            await ContractPlanChangeEmails.SendToClientAsync(
                _context,
                _emailService,
                _emailRenderer,
                _configuration,
                _logger,
                contract,
                clientUserId,
                freelancerProfile.UserId,
                changeReason,
                cancellationToken);
        }

        var participantUserIds = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(p => p.Conversations.ContractsId == contract.ContractsId && p.LeftAt == null && p.DeletedAt == null)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (participantUserIds.Any())
        {
            await _chatRealtimeNotifier.SendUsersEventAsync(
                [.. participantUserIds],
                "ContractDetailsChangeRequested",
                new { contractId = contract.ContractsId },
                cancellationToken);
        }

        var escrowId = await _context.Set<ContractEscrow>()
            .Where(escrow => escrow.ContractsId == contract.ContractsId)
            .Select(escrow => (Guid?)escrow.ContractEscrowId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ContractWorkflowResponse(contract.ContractsId, contract.Status, escrowId, null);
    }
}
