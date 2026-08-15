using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Disputes.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.ReviewEvidence.Commands;

public sealed class ReviewDisputeEvidenceCommandHandler : IRequestHandler<ReviewDisputeEvidenceCommand, DisputeEvidenceResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly INotificationService _notifications;
    private readonly IChatRealtimeNotifier _realtime;
    private readonly ILogger<ReviewDisputeEvidenceCommandHandler> _logger;

    public ReviewDisputeEvidenceCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        INotificationService notifications,
        IChatRealtimeNotifier realtime,
        ILogger<ReviewDisputeEvidenceCommandHandler> logger)
    {
        _context = context;
        _clock = clock;
        _notifications = notifications;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<DisputeEvidenceResponse> Handle(ReviewDisputeEvidenceCommand command, CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        var evidence = await _context.Set<DisputeEvidence>()
            .Include(item => item.Disputes).ThenInclude(item => item.Contracts)
            .FirstOrDefaultAsync(item =>
                    item.DisputeEvidenceId == command.EvidenceId && item.DisputesId == command.DisputeId,
                cancellationToken)
            ?? throw new NotFoundException("Dispute evidence does not exist.");

        if (evidence.Disputes.AssignedAdminId != command.AdminId)
            throw new ForbiddenAccessException("Only the assigned administrator may review evidence for this dispute.");

        if (!evidence.UploadedById.HasValue || string.IsNullOrWhiteSpace(evidence.FileUrl))
            throw new BadRequestException("Unfulfilled evidence requests cannot be reviewed.");

        var now = _clock.UtcNow;
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        evidence.ReviewedByAdminId = command.AdminId;
        evidence.ReviewedAt = now;
        evidence.ReviewNote = string.IsNullOrWhiteSpace(command.ReviewNote) ? null : command.ReviewNote.Trim();

        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = command.AdminId,
            Action = "Dispute.EvidenceReviewed",
            EntityId = evidence.DisputesId,
            EntityType = nameof(Dispute),
            NewValues = JsonSerializer.Serialize(new { evidence.DisputeEvidenceId, evidence.ReviewNote }),
            CreatedAt = now
        });

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(item => item.DisputesId == evidence.DisputesId, cancellationToken);
        var message = conversation is null
            ? null
            : ContractConversationEvents.AddSystemMessage(
                _context,
                conversation,
                $"Administrator reviewed evidence: {evidence.FileName}.",
                now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (message is not null && conversation is not null)
        {
            try
            {
                await _realtime.SendConversationEventAsync(
                    conversation.ConversationsId,
                    "ReceiveMessage",
                    new
                    {
                        messagesId = message.MessagesId,
                        conversationsId = message.ConversationsId,
                        senderUserId = (Guid?)null,
                        messageType = message.MessageType,
                        content = message.Content,
                        sentAt = message.SentAt,
                        attachments = Array.Empty<object>()
                    },
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to publish evidence-review event.");
            }
        }

        try
        {
            await _notifications.CreateNotificationAsync(
                evidence.UploadedById.Value,
                NotificationType.DisputeUpdate,
                "Dispute evidence reviewed",
                $"An administrator reviewed '{evidence.FileName}'.",
                evidence.Disputes.ContractsId,
                nameof(Contract),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Evidence review persisted but notification delivery failed.");
        }

        return new DisputeEvidenceResponse(
            evidence.DisputeEvidenceId,
            evidence.UploadedById,
            evidence.FileName,
            evidence.FileSize,
            evidence.Description,
            evidence.CreatedAt,
            evidence.IsRequestedByAdmin,
            evidence.RequestGroupId,
            evidence.RequestedByAdminId,
            evidence.RequestedAt,
            evidence.Deadline,
            evidence.RequestTarget,
            evidence.IsRequestFulfilled,
            evidence.ReviewedByAdminId,
            evidence.ReviewedAt,
            evidence.ReviewNote);
    }
}
