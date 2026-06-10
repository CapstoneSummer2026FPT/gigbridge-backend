using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Notification;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService? _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        INotificationSender notificationSender,
        ILogger<NotificationService> logger,
        IEmailService? emailService = null)
    {
        _context = context;
        _notificationSender = notificationSender;
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
        CancellationToken cancellationToken = default)
    {
        var notification = new Domain.Entities.Notification
        {
            UserId = userId,
            Type = (int)type,
            Title = title,
            Content = content,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<Domain.Entities.Notification>().Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationSender.SendToUserAsync(userId, MapToDto(notification), cancellationToken);
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
            _logger.LogWarning("Broadcast notification '{Title}' had no target users (target={Target}).", title, target);
            return;
        }

        var now = DateTime.UtcNow;
        var broadcastNotification = new BroadcastNotification
        {
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
                UserId = userId,
                IsRead = false,
                CreatedAt = now
            });
        }

        _context.Set<BroadcastNotification>().Add(broadcastNotification);
        await _context.SaveChangesAsync(cancellationToken);

        var sendTasks = broadcastNotification.Recipients.Select(r =>
            _notificationSender.SendToUserAsync(r.UserId, MapToDto(broadcastNotification, r), cancellationToken));
        await Task.WhenAll(sendTasks);

        if (sendEmail && _emailService is not null)
        {
            await SendBroadcastEmailAsync(targetUserIds, title, content ?? title, cancellationToken);
        }
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

    private static NotificationDto MapToDto(Domain.Entities.Notification notification)
    {
        return new NotificationDto
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
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }

    private static NotificationDto MapToDto(
        BroadcastNotification notification,
        BroadcastNotificationRecipient recipient)
    {
        return new NotificationDto
        {
            Id = notification.BroadcastNotificationId,
            Source = "Broadcast",
            BroadcastNotificationId = notification.BroadcastNotificationId,
            BroadcastRecipientId = recipient.BroadcastNotificationRecipientId,
            ReadTargetId = recipient.BroadcastNotificationRecipientId,
            Type = (NotificationType)notification.Type,
            Title = notification.Title,
            Content = notification.Content,
            ReferenceId = notification.ReferenceId,
            ReferenceType = notification.ReferenceType,
            IsRead = recipient.IsRead ?? false,
            ReadAt = recipient.ReadAt,
            CreatedAt = recipient.CreatedAt
        };
    }
}
