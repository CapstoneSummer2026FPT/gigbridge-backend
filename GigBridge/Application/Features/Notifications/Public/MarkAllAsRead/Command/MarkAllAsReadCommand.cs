using MediatR;

namespace Application.Features.Notifications.Commands.MarkAllAsRead;

public class MarkAllAsReadCommand : IRequest
{
    public Guid UserId { get; set; }
}
