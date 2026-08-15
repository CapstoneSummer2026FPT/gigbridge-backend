using Application.Common.InternalServices.Notifications.Models;
using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Disputes;
using Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.RequestEvidence.Commands;

public sealed class RequestEvidenceCommandHandler :
    IRequestHandler<RequestEvidenceCommand, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<RequestEvidenceCommandHandler> _logger;

    public RequestEvidenceCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationSender notificationSender,
        ILogger<RequestEvidenceCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        RequestEvidenceCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new BadRequestException("Evidence request reason is required.");
        if (!Enum.IsDefined(command.Target))
            throw new BadRequestException("Evidence request target is invalid.");

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts).ThenInclude(contract => contract.ClientProfiles)
            .Include(item => item.Contracts).ThenInclude(contract => contract.FreelancerProfiles)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.AssignedAdminId != command.AdminId)
            throw new ForbiddenAccessException("Only the assigned administrator may request evidence for this dispute.");

        if (dispute.Status is (int)DisputeStatus.Resolved or (int)DisputeStatus.Closed)
        {
            throw new BadRequestException("Evidence can only be requested while a dispute is active.");
        }

        if (!dispute.RespondentId.HasValue)
            throw new BadRequestException("The dispute respondent is not available.");

        var now = _dateTimeService.UtcNow;
        DateTime? deadlineUtc = null;
        if (command.Deadline.HasValue)
        {
            deadlineUtc = command.Deadline.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(command.Deadline.Value, DateTimeKind.Utc)
                : command.Deadline.Value.ToUniversalTime();

            if (deadlineUtc.Value <= now)
                throw new BadRequestException("Evidence request deadline must be in the future.");
        }

        var groupId = Guid.NewGuid();
        var targets = command.Target == EvidenceRequestTarget.Both
            ? new[] { EvidenceRequestTarget.Reporter, EvidenceRequestTarget.Respondent }
            : new[] { command.Target };
        var reason = command.Reason.Trim();
        var placeholders = targets.Select(target => new DisputeEvidence
        {
            DisputeEvidenceId = Guid.NewGuid(),
            DisputesId = dispute.DisputesId,
            UploadedById = null,
            FileName = null,
            FileUrl = null,
            FileSize = null,
            Description = reason,
            IsRequestedByAdmin = true,
            RequestGroupId = groupId,
            RequestedByAdminId = command.AdminId,
            RequestedAt = now,
            Deadline = deadlineUtc,
            RequestTarget = (int)target,
            IsRequestFulfilled = false,
            CreatedAt = now
        }).ToList();

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        _context.Set<DisputeEvidence>().AddRange(placeholders);
        dispute.Status = (int)DisputeStatus.InProgress;
        dispute.UpdatedAt = now;

        var targetLabel = command.Target switch
        {
            EvidenceRequestTarget.Reporter => "reporter",
            EvidenceRequestTarget.Respondent => "respondent",
            _ => "reporter and respondent"
        };
        var deadlineText = deadlineUtc.HasValue
            ? $" by {deadlineUtc.Value:yyyy-MM-dd}"
            : string.Empty;
        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(item => item.DisputesId == dispute.DisputesId, cancellationToken);
        var systemMessage = conversation is null
            ? null
            : ContractConversationEvents.AddSystemMessage(
                _context,
                conversation,
                $"Additional evidence requested from the {targetLabel}: {reason}{deadlineText}.",
                now);

        _context.Set<AdminAuditLog>().Add(new AdminAuditLog
        {
            AdminAuditLogsId = Guid.NewGuid(),
            AdminId = command.AdminId,
            Action = "Dispute.RequestEvidence",
            EntityId = dispute.DisputesId,
            EntityType = nameof(Dispute),
            NewValues = JsonSerializer.Serialize(new
            {
                groupId,
                target = command.Target.ToString(),
                reason,
                command.Deadline,
                evidenceIds = placeholders.Select(item => item.DisputeEvidenceId)
            }),
            CreatedAt = now
        });

        var recipientIds = targets
            .Select(target => target == EvidenceRequestTarget.Reporter
                ? dispute.InitiatorId
                : dispute.RespondentId!.Value)
            .Distinct()
            .ToList();
        var notifications = recipientIds.Select(userId => new Notification
        {
            NotificationsId = Guid.NewGuid(),
            UserId = userId,
            Type = (int)NotificationType.DisputeUpdate,
            Title = "Additional dispute evidence requested",
            Content = $"An administrator requested additional evidence for contract '{dispute.Contracts.Title}'{deadlineText}.",
            ReferenceId = dispute.ContractsId,
            ReferenceType = nameof(Contract),
            IsRead = false,
            CreatedAt = now
        }).ToList();
        _context.Set<Notification>().AddRange(notifications);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (systemMessage is not null && conversation is not null)
        {
            try
            {
                await _chatRealtimeNotifier.SendConversationEventAsync(
                    conversation.ConversationsId,
                    "ReceiveMessage",
                    BuildSystemMessagePayload(systemMessage),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to publish evidence-request conversation event.");
            }
        }

        foreach (var notification in notifications)
        {
            try
            {
                await _notificationSender.SendToUserAsync(
                    notification.UserId,
                    ToNotificationDto(notification),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to deliver evidence-request notification {NotificationId}.", notification.NotificationsId);
            }
        }

        return await AdminDisputeSupport.GetDetailAsync(_context, dispute.DisputesId, cancellationToken);
    }

    private static NotificationDto ToNotificationDto(Notification notification) => new()
    {
        Id = notification.NotificationsId,
        Source = "Personal",
        NotificationId = notification.NotificationsId,
        ReadTargetId = notification.NotificationsId,
        Type = (NotificationType)notification.Type,
        Title = notification.Title,
        Content = notification.Content,
        ReferenceId = notification.ReferenceId,
        ReferenceType = notification.ReferenceType,
        IsRead = notification.IsRead ?? false,
        CreatedAt = notification.CreatedAt
    };

    private static object BuildSystemMessagePayload(Message message) => new
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
