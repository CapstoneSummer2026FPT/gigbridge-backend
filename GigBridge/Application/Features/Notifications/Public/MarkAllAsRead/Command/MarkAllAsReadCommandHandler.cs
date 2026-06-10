using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Public.MarkAllAsRead.Command;

public class MarkAllAsReadCommandHandler : IRequestHandler<MarkAllAsReadCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkAllAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(MarkAllAsReadCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await _context.Set<Notification>()
            .Where(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), cancellationToken);

        await _context.Set<BroadcastNotificationRecipient>()
            .Where(r => r.UserId == request.UserId && (r.IsRead == null || r.IsRead == false))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IsRead, true)
                .SetProperty(r => r.ReadAt, now), cancellationToken);
    }
}
