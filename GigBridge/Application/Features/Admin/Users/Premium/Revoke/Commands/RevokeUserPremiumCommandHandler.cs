using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.Premium.Revoke.Commands;

public sealed class RevokeUserPremiumCommandHandler : IRequestHandler<RevokeUserPremiumCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly ICacheService _cache;
    private readonly INotificationService _notifications;

    public RevokeUserPremiumCommandHandler(IApplicationDbContext context, IDateTimeService clock, ICacheService cache, INotificationService notifications)
    {
        _context = context;
        _clock = clock;
        _cache = cache;
        _notifications = notifications;
    }

    public async Task<bool> Handle(RevokeUserPremiumCommand command, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var active = await _context.Set<Subscription>().Include(x => x.SubscriptionPlans)
            .Where(x => x.UserId == command.UserId &&
                        x.Status == SubscriptionStatus.Active && x.EndDate > now)
            .CompatibleWithRole(UserRole.Freelancer)
            .ToListAsync(ct);
        if (active.Count == 0) return false;

        foreach (var subscription in active)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.EndDate = now;
            subscription.CancelledAt = now;
            subscription.AutoRenew = false;
        }
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"premium:access:{command.UserId:N}", ct);
        await _notifications.CreateNotificationAsync(command.UserId, NotificationType.SubscriptionCancelled, "Freelancer Premium revoked by an administrator", null, null, nameof(Subscription), ct);
        return true;
    }
}
