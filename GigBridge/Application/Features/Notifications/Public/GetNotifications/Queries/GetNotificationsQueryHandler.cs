using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Notifications.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PaginatedList<NotificationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 20 ? 20 : request.PageSize;
        var now = DateTime.UtcNow;

        var personalEntityQuery = _context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId);

        var broadcastEntityQuery = _context.Set<BroadcastNotificationRecipient>()
            .AsNoTracking()
            .Where(r =>
                r.UserId == request.UserId &&
                (r.BroadcastNotification.ExpiresAt == null || r.BroadcastNotification.ExpiresAt > now));

        if (request.UnreadOnly)
        {
            personalEntityQuery = personalEntityQuery.Where(n => n.IsRead == null || n.IsRead == false);
            broadcastEntityQuery = broadcastEntityQuery.Where(r => r.IsRead == null || r.IsRead == false);
        }

        var personalQuery = personalEntityQuery
            .Select(n => new NotificationDto
            {
                Id = n.NotificationsId,
                Source = "Personal",
                NotificationId = n.NotificationsId,
                BroadcastNotificationId = null,
                BroadcastRecipientId = null,
                ReadTargetId = n.NotificationsId,
                Type = (NotificationType)n.Type,
                Title = n.Title,
                Content = n.Content,
                ReferenceId = n.ReferenceId,
                ReferenceType = n.ReferenceType,
                IsRead = n.IsRead ?? false,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            });

        var broadcastQuery = broadcastEntityQuery
            .Select(r => new NotificationDto
            {
                Id = r.BroadcastNotification.BroadcastNotificationId,
                Source = "Broadcast",
                NotificationId = null,
                BroadcastNotificationId = r.BroadcastNotification.BroadcastNotificationId,
                BroadcastRecipientId = r.BroadcastNotificationRecipientId,
                ReadTargetId = r.BroadcastNotificationRecipientId,
                Type = (NotificationType)r.BroadcastNotification.Type,
                Title = r.BroadcastNotification.Title,
                Content = r.BroadcastNotification.Content,
                ReferenceId = r.BroadcastNotification.ReferenceId,
                ReferenceType = r.BroadcastNotification.ReferenceType,
                IsRead = r.IsRead ?? false,
                ReadAt = r.ReadAt,
                CreatedAt = r.CreatedAt
            });

        var totalCount = await personalQuery.CountAsync(cancellationToken)
            + await broadcastQuery.CountAsync(cancellationToken);

        var items = await personalQuery
            .Concat(broadcastQuery)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<NotificationDto>(items, totalCount, pageNumber, pageSize);
    }
}
