using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Users.Premium.Grant.Commands;

public sealed class GrantUserPremiumCommandHandler : IRequestHandler<GrantUserPremiumCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly ICacheService _cache;
    private readonly INotificationService _notifications;

    public GrantUserPremiumCommandHandler(IApplicationDbContext context, IDateTimeService clock, ICacheService cache, INotificationService notifications)
    {
        _context = context;
        _clock = clock;
        _cache = cache;
        _notifications = notifications;
    }

    public async Task<bool> Handle(GrantUserPremiumCommand command, CancellationToken ct)
    {
        var user = await _context.Set<User>().FirstOrDefaultAsync(x => x.UserId == command.UserId, ct)
            ?? throw new NotFoundException("User", command.UserId);
        if (user.Role != (int)UserRole.Client && user.Role != (int)UserRole.Freelancer)
            throw new BadRequestException("Only client and freelancer accounts can receive Premium.");

        var targetRole = (UserRole)user.Role;
        var premiumName = targetRole == UserRole.Client ? "Client Premium" : "Freelancer Premium";

        var now = _clock.UtcNow;
        var alreadyPremium = await _context.Set<Subscription>().AnyAsync(x =>
            x.UserId == command.UserId &&
            x.Status == SubscriptionStatus.Active &&
            x.StartDate <= now &&
            x.EndDate > now &&
            x.SubscriptionPlans.IsActive == true &&
            x.SubscriptionPlans.Price > 0 &&
            (x.SubscriptionPlans.TargetRole == null || x.SubscriptionPlans.TargetRole == user.Role), ct);
        if (alreadyPremium) return false;

        var plan = await _context.Set<SubscriptionPlan>()
            .Where(x => x.IsActive == true && x.Price > 0 &&
                (x.TargetRole == null || x.TargetRole == user.Role))
            .OrderByDescending(x => x.TargetRole == user.Role)
            .ThenByDescending(x => x.DurationInDays)
            .ThenBy(x => x.Price)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"No active {premiumName} plan is configured.");
        var subscription = new Subscription { SubscriptionsId = Guid.NewGuid(), UserId = command.UserId, SubscriptionPlansId = plan.SubscriptionPlansId, Status = SubscriptionStatus.Active, StartDate = now, EndDate = now.AddDays(365), AutoRenew = false, PaymentReference = $"ADMIN-GRANT-{Guid.NewGuid():N}", CreatedAt = now };
        _context.Set<Subscription>().Add(subscription);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveAsync(GetPremiumCacheKey(targetRole, command.UserId), ct);
        await _notifications.CreateNotificationAsync(command.UserId, NotificationType.SubscriptionActivated, $"{premiumName} granted through {subscription.EndDate:yyyy-MM-dd}", null, subscription.SubscriptionsId, nameof(Subscription), ct);
        return true;
    }

    private static string GetPremiumCacheKey(UserRole role, Guid userId) =>
        role == UserRole.Client
            ? $"premium:access:client:{userId:N}"
            : $"premium:access:{userId:N}";
}
