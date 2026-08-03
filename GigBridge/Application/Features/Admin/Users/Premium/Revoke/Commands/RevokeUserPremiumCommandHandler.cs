using Application.Common.Exceptions;
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
        var user = await _context.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == command.UserId, ct)
            ?? throw new NotFoundException("User", command.UserId);
        if (user.Role != (int)UserRole.Client && user.Role != (int)UserRole.Freelancer)
            throw new BadRequestException("Only client and freelancer accounts can have Premium revoked.");

        var targetRole = (UserRole)user.Role;
        var premiumName = targetRole == UserRole.Client ? "Client Premium" : "Freelancer Premium";
        var now = _clock.UtcNow;
        var active = await _context.Set<Subscription>().Include(x => x.SubscriptionPlans)
            .Where(x =>
                x.UserId == command.UserId &&
                x.Status == SubscriptionStatus.Active &&
                x.StartDate <= now &&
                x.EndDate > now &&
                x.SubscriptionPlans.IsActive == true &&
                x.SubscriptionPlans.Price > 0 &&
                (x.SubscriptionPlans.TargetRole == null || x.SubscriptionPlans.TargetRole == user.Role))
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
        await _cache.RemoveAsync(GetPremiumCacheKey(targetRole, command.UserId), ct);
        await _notifications.CreateNotificationAsync(command.UserId, NotificationType.SubscriptionCancelled, $"{premiumName} revoked by an administrator", null, null, nameof(Subscription), ct);
        return true;
    }

    private static string GetPremiumCacheKey(UserRole role, Guid userId) =>
        role == UserRole.Client
            ? $"premium:access:client:{userId:N}"
            : $"premium:access:{userId:N}";
}
