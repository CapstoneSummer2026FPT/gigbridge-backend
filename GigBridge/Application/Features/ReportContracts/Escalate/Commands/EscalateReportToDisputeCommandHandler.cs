using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.ReportContracts.Escalate.Commands;

public sealed class EscalateReportToDisputeCommandHandler :
    IRequestHandler<EscalateReportToDisputeCommand, DisputeResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly ILogger<EscalateReportToDisputeCommandHandler> _logger;

    public EscalateReportToDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger<EscalateReportToDisputeCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _logger = logger;
    }

    public async Task<DisputeResponse> Handle(
        EscalateReportToDisputeCommand command,
        CancellationToken cancellationToken)
    {
        // Load contract
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        // Validate user is a participant
        var participants = await DisputeAccess.EnsureParticipantAsync(
            _context, contract, command.UserId, cancellationToken);

        // Load the report
        var report = await _context.Set<ReportContract>()
            .FirstOrDefaultAsync(r => r.ReportContractId == command.ReportId, cancellationToken)
            ?? throw new NotFoundException("Report does not exist.");

        if (report.ContractId != command.ContractId)
        {
            throw new BadRequestException("The report does not belong to this contract.");
        }

        // Only the reporter can escalate
        if (report.ReporterId != command.UserId)
        {
            throw new ForbiddenAccessException("Only the reporter can escalate the report to a dispute.");
        }

        // Report must be waiting for confirmation
        if (report.Status != (int)ContractReportStatus.WaitingReporterConfirmation)
        {
            throw new BadRequestException("Only reports with a declined resolution can be escalated.");
        }

        // Check no active dispute exists
        var hasActiveDispute = await _context.Set<Dispute>()
            .AnyAsync(d =>
                d.ContractsId == command.ContractId &&
                DisputeAccess.ActiveStatuses.Contains(d.Status),
                cancellationToken);

        if (hasActiveDispute)
        {
            throw new ConflictException("An active dispute already exists for this contract.");
        }

        // Contract must be in an eligible status
        DisputeAccess.EnsureCreationAllowed(contract);

        var now = _dateTimeService.UtcNow;
        var initiatorName = await _context.Set<User>()
            .AsNoTracking()
            .Where(u => u.UserId == command.UserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        // Determine respondent: use report's respondent if set, otherwise the other party
        var resolvedRespondentId = report.RespondentId ?? participants.GetOtherParty(command.UserId);

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

        // Create the dispute
        var dispute = new Dispute
        {
            DisputesId = Guid.NewGuid(),
            ContractsId = command.ContractId,
            InitiatorId = command.UserId,
            RespondentId = resolvedRespondentId,
            MilestonesId = report.MilestoneId,
            RelatedReportId = command.ReportId,
            Title = command.Title?.Trim(),
            Description = command.Description?.Trim(),
            Reason = command.Reason.Trim(),
            ClaimedAmount = command.ClaimedAmount,
            RequestedResolution = command.RequestedResolution?.Trim(),
            Status = (int)DisputeStatus.Open,
            Resolution = null,
            ResolutionNote = null,
            ResolvedByAdminId = null,
            ResolvedAt = null,
            CreatedAt = now,
            UpdatedAt = null,
            OpenedAt = now
        };

        _context.Set<Dispute>().Add(dispute);

        // Mark report as escalated
        report.Status = (int)ContractReportStatus.Escalated;
        report.IsEscalatedToDispute = true;

        // Lock the contract
        contract.Status = (int)ContractStatus.Disputed;

        // Create dispute conversation
        var conversation = new Conversation
        {
            ConversationsId = Guid.NewGuid(),
            ConversationType = (int)ConversationType.Dispute,
            Title = $"Dispute: {contract.Title}",
            ContractsId = command.ContractId,
            DisputesId = dispute.DisputesId,
            CreatedByUserId = command.UserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Set<Conversation>().Add(conversation);

        // Add participants (Client + Freelancer)
        var conversationParticipants = new List<ConversationParticipant>
        {
            new()
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversation.ConversationsId,
                UserId = participants.ClientUserId,
                ParticipantRole = (int)ParticipantRole.Client,
                JoinedAt = now,
                UnreadCount = 0
            }
        };

        if (participants.FreelancerUserId.HasValue)
        {
            conversationParticipants.Add(new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = conversation.ConversationsId,
                UserId = participants.FreelancerUserId.Value,
                ParticipantRole = (int)ParticipantRole.Freelancer,
                JoinedAt = now,
                UnreadCount = 0
            });
        }

        _context.Set<ConversationParticipant>().AddRange(conversationParticipants);

        // Persist the conversation before setting LastMessageId. Adding both the new
        // conversation and its first message in one save creates an EF dependency cycle.
        await _context.SaveChangesAsync(cancellationToken);

        // Insert system message
        var systemMessage = ContractConversationEvents.AddSystemMessage(
            _context,
            conversation,
            "A dispute has been opened.",
            now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Send real-time notification for system message
        if (systemMessage is not null)
        {
            try
            {
                await _chatRealtimeNotifier.SendConversationEventAsync(
                    conversation.ConversationsId,
                    "ReceiveMessage",
                    BuildSystemMessagePayload(systemMessage),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Failed to send real-time system message for dispute {DisputeId}.",
                    dispute.DisputesId);
            }
        }

        // Notify the other party
        var otherPartyId = participants.GetOtherParty(command.UserId);
        if (otherPartyId.HasValue)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(
                    otherPartyId.Value,
                    NotificationType.DisputeUpdate,
                    "A dispute has been opened",
                    $"A dispute has been opened on contract '{contract.Title}'.",
                    contract.ContractsId,
                    nameof(Contract),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Dispute {DisputeId} was created, but notification delivery to user {UserId} failed.",
                    dispute.DisputesId,
                    otherPartyId.Value);
            }
        }

        return BuildDisputeResponse(
            dispute,
            initiatorName,
            participants.GetRole(command.UserId),
            resolvedRespondentId.HasValue ? participants.GetRole(resolvedRespondentId.Value) : null,
            null);
    }

    private static DisputeResponse BuildDisputeResponse(
        Dispute dispute,
        string? initiatorName,
        string? initiatorRole,
        string? respondentRole,
        string? milestoneTitle)
    {
        return new DisputeResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.InitiatorId,
            initiatorName,
            initiatorRole,
            dispute.RespondentId,
            null,
            respondentRole,
            dispute.MilestonesId,
            milestoneTitle,
            dispute.RelatedReportId,
            dispute.Title,
            dispute.Description,
            dispute.Reason,
            dispute.ClaimedAmount,
            dispute.RequestedResolution,
            dispute.Status,
            dispute.Resolution,
            null,
            dispute.ResolutionNote,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.OpenedAt,
            Array.Empty<DisputeEvidenceResponse>());
    }

    private static object BuildSystemMessagePayload(Domain.Entities.Message message)
    {
        return new
        {
            messagesId = message.MessagesId,
            conversationsId = message.ConversationsId,
            senderUserId = (Guid?)null,
            messageType = message.MessageType,
            content = message.Content,
            replyToMessageId = (Guid?)null,
            metadata = (string?)null,
            clientMessageId = (string?)null,
            sentAt = message.SentAt,
            editedAt = (DateTime?)null,
            isDeleted = false,
            attachments = Array.Empty<object>()
        };
    }
}
