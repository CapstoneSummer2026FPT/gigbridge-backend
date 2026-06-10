using Application.Features.Notifications.Common.DTOs;
using MediatR;

namespace Application.Features.Notifications.Queries.GetUnreadCount;

public class GetUnreadCountQuery : IRequest<UnreadCountResponse>
{
    public Guid UserId { get; set; }
}
