using MediatR;

namespace Application.Features.Notifications.Public.DeleteNotification.Command;

public class DeleteNotificationCommand : IRequest
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
}
