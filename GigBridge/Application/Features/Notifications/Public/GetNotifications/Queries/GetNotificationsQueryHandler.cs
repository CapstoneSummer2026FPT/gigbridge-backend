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

        var query = _context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId);

        if (request.UnreadOnly)
        {
            query = query.Where(n => n.IsRead == null || n.IsRead == false);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.NotificationsId,
                Type = (NotificationType)n.Type,
                Title = n.Title,
                Content = n.Content,
                ReferenceId = n.ReferenceId,
                ReferenceType = n.ReferenceType,
                IsRead = n.IsRead ?? false,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<NotificationDto>(items, totalCount, pageNumber, pageSize);
    }
}
