using MediatR;

namespace Application.Features.Notifications.Public.MarkBroadcastAsRead.Command;

public class MarkBroadcastAsReadCommand : IRequest
{
    public Guid BroadcastRecipientId { get; set; }
    public Guid UserId { get; set; }
}
