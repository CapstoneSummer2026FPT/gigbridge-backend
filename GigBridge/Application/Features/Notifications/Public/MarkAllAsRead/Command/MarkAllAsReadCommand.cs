using MediatR;

namespace Application.Features.Notifications.Public.MarkAllAsRead.Command;

public class MarkAllAsReadCommand : IRequest
{
    public Guid UserId { get; set; }
}
