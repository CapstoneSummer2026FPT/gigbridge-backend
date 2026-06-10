using Application.Features.Notifications.Common.DTOs;

namespace Application.Common.Interfaces.IService;

public interface INotificationSender
{
    Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
