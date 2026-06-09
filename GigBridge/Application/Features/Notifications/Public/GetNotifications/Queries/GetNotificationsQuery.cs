using Application.Common.Models;
using Application.Features.Notifications.Common.DTOs;
using MediatR;

namespace Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQuery : PaginatedQuery, IRequest<PaginatedList<NotificationDto>>
{
    public Guid UserId { get; set; }
    public bool UnreadOnly { get; set; }
}
