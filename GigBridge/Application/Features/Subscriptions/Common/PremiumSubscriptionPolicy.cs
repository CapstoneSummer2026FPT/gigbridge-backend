using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Subscriptions.Common;

public static class PremiumSubscriptionPolicy
{
    private const long PurchaseLockNamespace = 0x5072656D69756D00;

    public static IQueryable<Subscription> CompatibleWithRole(
        this IQueryable<Subscription> subscriptions,
        UserRole role) =>
        subscriptions.Where(subscription =>
            subscription.SubscriptionPlans.IsActive == true &&
            subscription.SubscriptionPlans.Price > 0 &&
            (subscription.SubscriptionPlans.TargetRole == null ||
             subscription.SubscriptionPlans.TargetRole == (int)role));

    public static IQueryable<Subscription> CompatibleWithUserRole(
        this IQueryable<Subscription> subscriptions) =>
        subscriptions.Where(subscription =>
            subscription.SubscriptionPlans.IsActive == true &&
            subscription.SubscriptionPlans.Price > 0 &&
            (subscription.User.Role == (int)UserRole.Client ||
             subscription.User.Role == (int)UserRole.Freelancer) &&
            (subscription.SubscriptionPlans.TargetRole == null ||
             subscription.SubscriptionPlans.TargetRole == subscription.User.Role));

    public static IQueryable<Subscription> EffectiveAt(
        this IQueryable<Subscription> subscriptions,
        UserRole role,
        DateTime now) =>
        subscriptions.CompatibleWithRole(role).Where(subscription =>
            subscription.Status == SubscriptionStatus.Active &&
            subscription.StartDate <= now &&
            subscription.EndDate > now);

    public static long PurchaseLockKey(Guid userId)
    {
        var bytes = userId.ToByteArray();
        return BitConverter.ToInt64(bytes, 0) ^
               BitConverter.ToInt64(bytes, 8) ^
               PurchaseLockNamespace;
    }
}
