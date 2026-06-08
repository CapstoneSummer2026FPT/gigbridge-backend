using MediatR;

namespace Application.Features.Notifications.Commands.MarkAsRead;

public class MarkAsReadCommand : IRequest
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
}
