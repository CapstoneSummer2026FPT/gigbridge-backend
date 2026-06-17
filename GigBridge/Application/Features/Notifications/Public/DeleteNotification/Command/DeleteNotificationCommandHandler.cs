using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Public.DeleteNotification.Command;

public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteNotificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = await _context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.NotificationsId == request.NotificationId, cancellationToken);

        if (notification is null)
        {
            throw new NotFoundException("Notification", request.NotificationId);
        }

        if (notification.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You do not have permission to delete this notification.");
        }

        _context.Set<Notification>().Remove(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
