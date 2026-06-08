using Application.Features.Notifications.Common.DTOs;

namespace Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsResponse
{
    public IEnumerable<NotificationDto> Items { get; set; } = Enumerable.Empty<NotificationDto>();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
