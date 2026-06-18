using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Notifications.Delete;

public class DeleteAdminNotificationCommandHandler : IRequestHandler<DeleteAdminNotificationCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteAdminNotificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteAdminNotificationCommand request, CancellationToken cancellationToken)
    {
        var broadcastNotification = await _context.Set<BroadcastNotification>()
            .FirstOrDefaultAsync(n => n.BroadcastNotificationId == request.NotificationId, cancellationToken);

        if (broadcastNotification is not null)
        {
            _context.Set<BroadcastNotification>().Remove(broadcastNotification);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var personalNotification = await _context.Set<Notification>()
            .FirstOrDefaultAsync(n => n.NotificationsId == request.NotificationId, cancellationToken);

        if (personalNotification is null)
        {
            throw new NotFoundException("Notification", request.NotificationId);
        }

        _context.Set<Notification>().Remove(personalNotification);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
