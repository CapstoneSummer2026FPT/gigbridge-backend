using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Subscriptions.Common;
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
        if (user.Role != (int)UserRole.Freelancer)
            throw new BadRequestException("Only freelancer accounts can receive Freelancer Premium.");

        var now = _clock.UtcNow;
        var alreadyPremium = await _context.Set<Subscription>()
            .Where(x => x.UserId == command.UserId)
            .EffectiveAt(UserRole.Freelancer, now)
            .AnyAsync(ct);
        if (alreadyPremium) return false;

        var plan = await _context.Set<SubscriptionPlan>().Where(x => x.IsActive == true &&
                (x.TargetRole == null || x.TargetRole == (int)UserRole.Freelancer) && x.Price > 0)
            .OrderByDescending(x => x.DurationInDays).ThenBy(x => x.Price).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("No active Freelancer Premium plan is configured.");
        var subscription = new Subscription { SubscriptionsId = Guid.NewGuid(), UserId = command.UserId, SubscriptionPlansId = plan.SubscriptionPlansId, SubscriptionPlans = plan, Status = SubscriptionStatus.Active, StartDate = now, EndDate = now.AddDays(plan.DurationInDays), AutoRenew = false, PaymentReference = $"ADMIN-GRANT-{Guid.NewGuid():N}", CreatedAt = now };
        _context.Set<Subscription>().Add(subscription);
        await _context.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"premium:access:{command.UserId:N}", ct);
        await _notifications.CreateNotificationAsync(command.UserId, NotificationType.SubscriptionActivated, $"Freelancer Premium granted through {subscription.EndDate:yyyy-MM-dd}", null, subscription.SubscriptionsId, nameof(Subscription), ct);
        return true;
    }
}
