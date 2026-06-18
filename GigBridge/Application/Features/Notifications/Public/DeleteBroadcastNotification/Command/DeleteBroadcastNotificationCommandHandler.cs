using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Public.DeleteBroadcastNotification.Command;

public class DeleteBroadcastNotificationCommandHandler : IRequestHandler<DeleteBroadcastNotificationCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteBroadcastNotificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteBroadcastNotificationCommand request, CancellationToken cancellationToken)
    {
        var recipient = await _context.Set<BroadcastNotificationRecipient>()
            .FirstOrDefaultAsync(r => r.BroadcastNotificationRecipientId == request.BroadcastRecipientId, cancellationToken);

        if (recipient is null)
        {
            throw new NotFoundException("BroadcastNotificationRecipient", request.BroadcastRecipientId);
        }

        if (recipient.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("You do not have permission to delete this notification.");
        }

        _context.Set<BroadcastNotificationRecipient>().Remove(recipient);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
