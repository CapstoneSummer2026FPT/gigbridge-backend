using Application.Common.InternalServices.Notifications.Models;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Email;
using Application.Common.InternalServices.Delivery.Models;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Auth.Shared.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
using Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Application.Common.Models.Email;

namespace Application.Common.InternalServices.Notifications.Services;
public class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IEmailService? _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        ILogger<NotificationService> logger,
        IEmailService? emailService = null)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string? content = null,
        Guid? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default,
        string? metadata = null)
    {
        var notification = new Domain.Entities.Notification
        {
            NotificationsId = Guid.NewGuid(),
            UserId = userId,
            Type = (int)type,
            Title = title,
            Content = content,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            Metadata = metadata,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Domain.Entities.Notification>().Add(notification);
        EnqueueRealtimeDelivery(
            $"notification:{notification.NotificationsId}",
            userId,
            new GenericNotificationDeliveryPayload(userId, notification.NotificationsId, null, null));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateBroadcastNotificationAsync(
        NotificationTarget target,
        NotificationType type,
        string title,
        string? content = null,
        Guid? referenceId = null,
        string? referenceType = null,
        Guid? targetUserId = null,
        bool sendEmail = false,
        Guid? createdByAdminId = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var targetUserIds = (await ResolveTargetUserIdsAsync(target, targetUserId, cancellationToken))
            .Distinct()
            .ToList();

        if (targetUserIds.Count == 0)
        {
            _logger.LogWarning("Broadcast notification had no target users (target={Target}).", target);
            return;
        }

        var now = DateTime.UtcNow;
        var broadcastNotification = new BroadcastNotification
        {
            BroadcastNotificationId = Guid.NewGuid(),
            Title = title,
            Content = content,
            Type = (int)type,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            TargetScope = (int)target,
            TargetRole = ResolveTargetRole(target),
            CreatedByAdminId = createdByAdminId,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        foreach (var userId in targetUserIds)
        {
            broadcastNotification.Recipients.Add(new BroadcastNotificationRecipient
            {
                BroadcastNotificationRecipientId = Guid.NewGuid(),
                UserId = userId,
                IsRead = false,
                CreatedAt = now
            });
        }

        _context.Set<BroadcastNotification>().Add(broadcastNotification);
        foreach (var recipient in broadcastNotification.Recipients)
        {
            EnqueueRealtimeDelivery(
                $"broadcast:{broadcastNotification.BroadcastNotificationId}:{recipient.BroadcastNotificationRecipientId}",
                recipient.UserId,
                new GenericNotificationDeliveryPayload(
                    recipient.UserId, null, broadcastNotification.BroadcastNotificationId, recipient.BroadcastNotificationRecipientId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (sendEmail && _emailService is not null)
        {
            await SendBroadcastEmailAsync(targetUserIds, title, content ?? title, cancellationToken);
        }
    }

    private void EnqueueRealtimeDelivery(string deliveryKey, Guid userId, GenericNotificationDeliveryPayload payload)
    {
        var now = DateTime.UtcNow;
        _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
        {
            DeliveryOutboxId = Guid.NewGuid(),
            DeliveryKey = deliveryKey,
            ScheduleId = null,
            DeliveryType = (int)DeliveryOutboxType.GenericNotification,
            RecipientUserId = userId,
            EventSequence = 0,
            Channel = (int)DeliveryChannel.NotificationRealtime,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Status = (int)DeliveryOutboxStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = now,
            CreatedAt = now
        });
    }

    private async Task<List<Guid>> ResolveTargetUserIdsAsync(
        NotificationTarget target,
        Guid? targetUserId,
        CancellationToken cancellationToken)
    {
        return target switch
        {
            NotificationTarget.User when targetUserId.HasValue => new List<Guid> { targetUserId.Value },
            NotificationTarget.User => new List<Guid>(),
            NotificationTarget.All => await _context.Set<User>()
                .Where(u => u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken),
            NotificationTarget.Clients => await _context.Set<User>()
                .Where(u => u.IsActive && u.Role == (int)UserRole.Client)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken),
            NotificationTarget.Freelancers => await _context.Set<User>()
                .Where(u => u.IsActive && u.Role == (int)UserRole.Freelancer)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken),
            NotificationTarget.Admins => await _context.Set<User>()
                .Where(u => u.IsActive && u.Role == (int)UserRole.Admin)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken),
            _ => new List<Guid>()
        };
    }

    private static int? ResolveTargetRole(NotificationTarget target)
    {
        return target switch
        {
            NotificationTarget.Clients => (int)UserRole.Client,
            NotificationTarget.Freelancers => (int)UserRole.Freelancer,
            NotificationTarget.Admins => (int)UserRole.Admin,
            _ => null
        };
    }

    private async Task SendBroadcastEmailAsync(
        List<Guid> userIds,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var users = await _context.Set<User>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId) && u.IsActive)
            .Select(u => new { u.UserId, u.Email })
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            try
            {
                await _emailService!.SendEmailAsync(new EmailRequest
                {
                    To = user.Email,
                    Subject = subject,
                    Body = body,
                    IsHtml = true
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send broadcast email to user {UserId}.", user.UserId);
            }
        }
    }

}
