using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Public.MarkBroadcastAsRead.Command;

public class MarkBroadcastAsReadCommandHandler : IRequestHandler<MarkBroadcastAsReadCommand>
{
    private readonly IApplicationDbContext _context;

    public MarkBroadcastAsReadCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(MarkBroadcastAsReadCommand request, CancellationToken cancellationToken)
    {
        var recipient = await _context.Set<BroadcastNotificationRecipient>()
            .FirstOrDefaultAsync(r => r.BroadcastNotificationRecipientId == request.BroadcastRecipientId, cancellationToken);

        if (recipient is null)
        {
            throw new NotFoundException("BroadcastNotificationRecipient", request.BroadcastRecipientId);
        }

        if (recipient.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You do not have permission to mark this notification as read.");
        }

        if (recipient.IsRead == true)
        {
            return;
        }

        recipient.IsRead = true;
        recipient.ReadAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
