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

        var unreadNotifications = await _context.Set<Notification>()
            .Where(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false))
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
