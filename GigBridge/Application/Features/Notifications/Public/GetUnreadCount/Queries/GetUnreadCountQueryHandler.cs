using Application.Common.Interfaces;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, UnreadCountResponse>
{
    private readonly IApplicationDbContext _context;

    public GetUnreadCountQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UnreadCountResponse> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var personalCount = await _context.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false), cancellationToken);

        var broadcastCount = await _context.Set<BroadcastNotificationRecipient>()
            .AsNoTracking()
            .CountAsync(r =>
                r.UserId == request.UserId &&
                (r.IsRead == null || r.IsRead == false) &&
                (r.BroadcastNotification.ExpiresAt == null || r.BroadcastNotification.ExpiresAt > now),
                cancellationToken);

        var count = personalCount + broadcastCount;

        return new UnreadCountResponse
        {
            UnreadCount = count
        };
    }
}
