using Application.Common.Interfaces;
using Application.Common.InternalServices.Realtime.Models;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Public.GetStatus.Queries;

public sealed class GetNotificationStatusQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetNotificationStatusQuery, RealtimeStatusResponse>
{
    public async Task<RealtimeStatusResponse> Handle(GetNotificationStatusQuery request, CancellationToken ct)
    {
        var state = await context.Set<UserRealtimeState>().AsNoTracking()
            .TagWith("Notification.Status")
            .Where(item => item.UserId == request.UserId)
            .Select(item => new RealtimeStatusResponse(item.NotificationRevision, item.NotificationUnreadCount))
            .SingleOrDefaultAsync(ct);
        if (state is not null) return state;

        var now = DateTime.UtcNow;
        var personal = await context.Set<Notification>().AsNoTracking()
            .CountAsync(item => item.UserId == request.UserId && item.IsRead != true, ct);
        var broadcast = await context.Set<BroadcastNotificationRecipient>().AsNoTracking()
            .CountAsync(item => item.UserId == request.UserId && item.IsRead != true &&
                (item.BroadcastNotification.ExpiresAt == null || item.BroadcastNotification.ExpiresAt > now), ct);
        return new RealtimeStatusResponse(0, personal + broadcast);
    }
}
