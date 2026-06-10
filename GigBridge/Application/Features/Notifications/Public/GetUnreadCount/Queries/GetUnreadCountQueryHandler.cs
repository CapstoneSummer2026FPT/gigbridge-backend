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
        var count = await _context.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false), cancellationToken);

        return new UnreadCountResponse
        {
            UnreadCount = count
        };
    }
}
