using MediatR;

namespace Application.Features.Admin.Notifications.Delete;

public class DeleteAdminNotificationCommand : IRequest
{
    public Guid NotificationId { get; set; }
}
