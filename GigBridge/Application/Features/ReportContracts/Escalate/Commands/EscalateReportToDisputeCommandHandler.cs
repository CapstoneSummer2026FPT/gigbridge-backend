using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Application.Features.ReportContracts.Common.Internal;
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
    private readonly IMediaService _mediaService;
    private readonly INotificationService _notificationService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly ILogger<EscalateReportToDisputeCommandHandler> _logger;
    private readonly IAdminAuditService? _audit;

    public EscalateReportToDisputeCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IMediaService mediaService,
        INotificationService notificationService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        ILogger<EscalateReportToDisputeCommandHandler> logger,
        IAdminAuditService? audit = null)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _mediaService = mediaService;
        _notificationService = notificationService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _logger = logger;
        _audit = audit;
    }

    public async Task<DisputeResponse> Handle(
        EscalateReportToDisputeCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(ReportContractLock.ForReport(command.ReportId), cancellationToken);
        await transaction.AcquireTransactionLockAsync(ContractEscrowLock.ForContract(command.ContractId), cancellationToken);

        // Reload all mutable state after acquiring both shared locks.
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        var report = await _context.Set<ReportContract>()
            .FirstOrDefaultAsync(r => r.ReportContractId == command.ReportId, cancellationToken)
            ?? throw new NotFoundException("Report does not exist.");

        if (report.ContractId != command.ContractId)
        {
            throw new BadRequestException("The report does not belong to this contract.");
        }

        var isAdminEscalation = command.AdminActorId.HasValue;
        if (isAdminEscalation)
        {
            var validAdmin = command.AdminActorId == command.UserId && await _context.Set<User>().AsNoTracking().AnyAsync(
                x => x.UserId == command.AdminActorId && x.Role == (int)UserRole.Admin && x.IsActive && x.AccountStatus == (int)AccountStatus.Active,
                cancellationToken);
            if (!validAdmin) throw new ForbiddenAccessException("An active administrator account is required.");
            if (string.IsNullOrWhiteSpace(command.AdminReason)) throw new BadRequestException("An escalation reason is required.");
            if (report.AdminReviewStatus is (int)ContractReportAdminStatus.Closed or (int)ContractReportAdminStatus.Dismissed or (int)ContractReportAdminStatus.Escalated or (int)ContractReportAdminStatus.LinkedToDispute)
                throw new ConflictException("This Contract Report is already finalized.");
            if (report.Status == (int)ContractReportStatus.Resolved)
                throw new ConflictException("A Contract Report resolved by its participants cannot be escalated.");
        }

        var initiatorId = report.ReporterId;
        var participants = await DisputeAccess.EnsureParticipantAsync(_context, contract, initiatorId, cancellationToken);

        // Only the reporter can escalate
        if (!isAdminEscalation && report.ReporterId != command.UserId)
        {
            throw new ForbiddenAccessException("Only the reporter can escalate the report to a dispute.");
        }

        // Report must be waiting for confirmation
        if (!isAdminEscalation && report.Status != (int)ContractReportStatus.WaitingReporterConfirmation)
        {
            throw new BadRequestException("Only reports with a declined resolution can be escalated.");
        }

        if (report.IssueType is (int)ContractReportIssueType.PaymentIssue
                or (int)ContractReportIssueType.MilestoneIssue &&
            (!command.ClaimedAmount.HasValue || command.ClaimedAmount <= 0))
        {
            throw new BadRequestException(
                "Claimed amount must be greater than 0 for payment or milestone disputes.");
        }

        if (!command.Urgency.HasValue || !Enum.IsDefined(command.Urgency.Value))
            throw new BadRequestException("Dispute urgency is required and must be valid.");

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
        var disputeId = Guid.NewGuid();
        var initiatorName = await _context.Set<User>()
            .AsNoTracking()
            .Where(u => u.UserId == initiatorId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        // Determine respondent: use report's respondent if set, otherwise the other party
        var resolvedRespondentId = report.RespondentId ?? participants.GetOtherParty(initiatorId);

        var reportAttachments = await _context.Set<ReportContractAttachment>()
            .AsNoTracking()
            .Where(attachment => attachment.ReportContractId == report.ReportContractId)
            .OrderBy(attachment => attachment.UploadedAt)
            .ToListAsync(cancellationToken);

        var evidences = reportAttachments
            .Select(attachment => new DisputeEvidence
            {
                DisputeEvidenceId = Guid.NewGuid(),
                DisputesId = disputeId,
                UploadedById = attachment.UploadedByUserId ?? report.ReporterId,
                FileName = attachment.FileName,
                FileUrl = attachment.FileUrl,
                FileSize = attachment.FileSize,
                Description = null,
                CreatedAt = attachment.UploadedAt
            })
            .ToList();

        if (command.EvidenceFiles.Count > 0)
        {
            DisputeEvidenceSupport.ValidateBatch(command.EvidenceFiles);
            foreach (var file in command.EvidenceFiles)
            {
                evidences.Add(await DisputeEvidenceSupport.UploadAsync(
                    _mediaService,
                    file,
                    disputeId,
                    initiatorId,
                    now,
                    cancellationToken));
            }
        }

        // Create the dispute
        var dispute = new Dispute
        {
            DisputesId = disputeId,
            ContractsId = command.ContractId,
            InitiatorId = initiatorId,
            RespondentId = resolvedRespondentId,
            MilestonesId = report.MilestoneId,
            RelatedReportId = command.ReportId,
            Title = command.Title.Trim(),
            Description = command.Description.Trim(),
            Reason = command.Description.Trim(),
            ClaimedAmount = command.ClaimedAmount,
            RequestedResolution = command.RequestedResolution.Trim(),
            Urgency = (int)command.Urgency.Value,
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
        if (evidences.Count > 0)
            _context.Set<DisputeEvidence>().AddRange(evidences);

        // Mark report as escalated
        report.Status = (int)ContractReportStatus.Escalated;
        report.IsEscalatedToDispute = true;
        report.UpdatedAt = now;
        if (isAdminEscalation)
        {
            report.AdminReviewStatus = (int)ContractReportAdminStatus.Escalated;
            report.AdminResolutionNote = command.AdminReason!.Trim();
            report.AssignedAdminId ??= command.AdminActorId;
            report.AssignedAt ??= now;
        }

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
            CreatedByUserId = initiatorId,
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

        if (isAdminEscalation)
            _audit?.Add(command.AdminActorId!.Value, AdminAuditActions.ContractReportEscalated, nameof(ReportContract), report.ReportContractId,
                new { status = (int)ContractReportStatus.WaitingReporterConfirmation, adminReviewStatus = (int)ContractReportAdminStatus.UnderReview },
                new { report.Status, report.AdminReviewStatus, disputeId = dispute.DisputesId, report.ContractId, report.ReporterId, report.RespondentId, reason = command.AdminReason });

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
        var otherPartyId = participants.GetOtherParty(initiatorId);
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
            participants.GetRole(initiatorId),
            resolvedRespondentId.HasValue ? participants.GetRole(resolvedRespondentId.Value) : null,
            null,
            report.IssueType,
            evidences);
    }

    private static DisputeResponse BuildDisputeResponse(
        Dispute dispute,
        string? initiatorName,
        string? initiatorRole,
        string? respondentRole,
        string? milestoneTitle,
        int? issueType,
        IReadOnlyList<DisputeEvidence> evidences)
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
            issueType,
            dispute.Urgency,
            dispute.Status,
            dispute.Resolution,
            null,
            dispute.ResolutionNote,
            dispute.AssignedAdminId,
            dispute.AssignedAt,
            dispute.ResolvedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.OpenedAt,
            evidences
                .OrderBy(evidence => evidence.CreatedAt)
                .Select(evidence => new DisputeEvidenceResponse(
                    evidence.DisputeEvidenceId,
                    evidence.UploadedById,
                    evidence.FileName,
                    evidence.FileSize,
                    evidence.Description,
                    evidence.CreatedAt))
                .ToList());
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
