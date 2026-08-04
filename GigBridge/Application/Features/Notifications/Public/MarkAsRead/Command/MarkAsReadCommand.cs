using MediatR;

namespace Application.Features.Notifications.Public.MarkAsRead.Command;

public class MarkAsReadCommand : IRequest
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public int? ExpectedRevision { get; set; }
}
