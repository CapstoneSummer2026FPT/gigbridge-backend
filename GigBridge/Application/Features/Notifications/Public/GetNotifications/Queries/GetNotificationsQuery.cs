using MediatR;

namespace Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQuery : IRequest<GetNotificationsResponse>
{
    public Guid UserId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool UnreadOnly { get; set; }
}
