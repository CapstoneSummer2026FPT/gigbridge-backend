using Application.Common.InternalServices.Notifications.Models;
using Application.Features.Notifications.Common.DTOs;

namespace Application.Common.InternalServices.Notifications.Interfaces;
public interface INotificationSender
{
    Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
