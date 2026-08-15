using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Contracts.Common.DTOs;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.MilestoneReview.Freelancer.RequestChange.Commands;

public sealed class RequestContractMilestoneChangeCommandHandler :
    IRequestHandler<RequestContractMilestoneChangeCommand, ContractWorkflowResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;

    public RequestContractMilestoneChangeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notificationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationService = notificationService;
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

        await ContractParticipantGuard.EnsureFreelancerAsync(_context, contract, command.UserId, cancellationToken);

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
            document.UpdatedAt = now;
        }

        var message = string.IsNullOrWhiteSpace(command.Request.Reason)
            ? "Milestone changes requested. Client must update contract details and both parties must sign again."
            : $"Milestone changes requested: {command.Request.Reason}";

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            message,
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (clientUserId != Guid.Empty)
        {
            var reason = command.Request.Reason?.Trim();
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
