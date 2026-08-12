using Application.Features.Notifications.Common.DTOs;

namespace Application.Features.Notifications.Common.Interfaces;

public interface INotificationSender
{
    Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
