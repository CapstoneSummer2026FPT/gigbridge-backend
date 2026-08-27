using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.InternalServices.Realtime.Models;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
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
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            RealtimeStateLockFor(request.UserId),
            cancellationToken,
            "Notification.MarkAllAsRead");

        int personalUpdated;
        int broadcastUpdated;
        if (_context.SupportsRelationalBulkOperations)
        {
            personalUpdated = await _context.Set<Notification>()
                .Where(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now), cancellationToken);

            broadcastUpdated = await _context.Set<BroadcastNotificationRecipient>()
                .Where(r => r.UserId == request.UserId && (r.IsRead == null || r.IsRead == false))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.IsRead, true)
                    .SetProperty(r => r.ReadAt, now), cancellationToken);
        }
        else
        {
            var personal = await _context.Set<Notification>()
                .Where(n => n.UserId == request.UserId && (n.IsRead == null || n.IsRead == false))
                .ToListAsync(cancellationToken);
            var broadcasts = await _context.Set<BroadcastNotificationRecipient>()
                .Where(r => r.UserId == request.UserId && (r.IsRead == null || r.IsRead == false))
                .ToListAsync(cancellationToken);
            personal.ForEach(item => { item.IsRead = true; item.ReadAt = now; });
            broadcasts.ForEach(item => { item.IsRead = true; item.ReadAt = now; });
            personalUpdated = personal.Count;
            broadcastUpdated = broadcasts.Count;
        }

        if (personalUpdated + broadcastUpdated > 0)
        {
            var state = await _context.Set<UserRealtimeState>()
                .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
            if (state is null)
            {
                state = new UserRealtimeState { UserId = request.UserId };
                _context.Set<UserRealtimeState>().Add(state);
            }

            state.NotificationRevision++;
            state.NotificationUnreadCount = 0;
            state.UpdatedAt = now;
            var payload = new NotificationStateChangedPayload(
                state.NotificationRevision,
                0,
                "reset");
            _context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
            {
                DeliveryOutboxId = Guid.NewGuid(),
                DeliveryKey = $"notification-state:{request.UserId:N}:{state.NotificationRevision}",
                DeliveryType = (int)DeliveryOutboxType.NotificationStateRevision,
                RecipientUserId = request.UserId,
                EventSequence = state.NotificationRevision,
                Channel = (int)DeliveryChannel.NotificationRealtime,
                Payload = JsonSerializer.Serialize(payload),
                Status = (int)DeliveryOutboxStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now
            });
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static long RealtimeStateLockFor(Guid userId) =>
        BitConverter.ToInt64(userId.ToByteArray(), 0) ^ 0x4E4F544946595254;
}
