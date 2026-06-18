using MediatR;

namespace Application.Features.Notifications.Public.DeleteBroadcastNotification.Command;

public class DeleteBroadcastNotificationCommand : IRequest
{
    public Guid BroadcastRecipientId { get; set; }
    public Guid UserId { get; set; }
}
