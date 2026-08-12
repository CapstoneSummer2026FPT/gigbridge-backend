using Domain.Enums.Notifications;

namespace Application.Features.Notifications.Common.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string? content = null,
        Guid? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default);

    Task CreateBroadcastNotificationAsync(
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
        CancellationToken cancellationToken = default);
}
