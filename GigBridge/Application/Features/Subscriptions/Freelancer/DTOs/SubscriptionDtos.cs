using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;

namespace Application.Features.Subscriptions.Freelancer.DTOs;

public static class PremiumPlanDefaults
{
    public static readonly Guid YearlyPromotionPlanId =
        Guid.Parse("95000000-0000-0000-0000-000000000003");
}

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    int DurationInDays,
    string? Features,
    int? SortOrder)
{
    public string BillingPeriod => DurationInDays >= 360 ? "yearly" : "monthly";

    public static SubscriptionPlanDto FromEntity(SubscriptionPlan plan)
    {
        var isLegacyVnd = string.Equals(plan.Currency, "VND", StringComparison.OrdinalIgnoreCase);
        return new SubscriptionPlanDto(
            plan.SubscriptionPlansId,
            plan.Name,
            plan.Description,
            isLegacyVnd ? TokenWalletRules.ToTokensCeiling(plan.Price) : plan.Price,
            isLegacyVnd ? "GigCoin" : plan.Currency ?? "GigCoin",
            plan.DurationInDays,
            plan.Features,
            plan.SortOrder);
    }
}

public sealed record SubscriptionDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    SubscriptionStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    bool AutoRenew,
    bool IsPremium,
    DateTime? CancelledAt,
    DateTime CreatedAt)
{
    public static SubscriptionDto FromEntity(Subscription subscription, DateTime now)
    {
        var effectiveStatus =
            subscription.Status == SubscriptionStatus.Active && subscription.EndDate <= now
                ? SubscriptionStatus.Expired
                : subscription.Status;

        return new SubscriptionDto(
            subscription.SubscriptionsId,
            subscription.SubscriptionPlansId,
            subscription.SubscriptionPlans.Name,
            effectiveStatus,
            subscription.StartDate,
            subscription.EndDate,
            subscription.AutoRenew ?? false,
            subscription.SubscriptionPlans.Price > 0,
            subscription.CancelledAt,
            subscription.CreatedAt);
    }
}
