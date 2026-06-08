using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.MarkAsRead;

public class MarkAsReadCommandHandler : IRequestHandler<MarkAsReadCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(MarkAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.NotificationsId == request.NotificationId, cancellationToken);

        if (notification is null)
        {
            throw new NotFoundException("Notification", request.NotificationId);
        }

        if (notification.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You do not have permission to mark this notification as read.");
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
