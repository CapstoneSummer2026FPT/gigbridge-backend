using Application.Features.Subscriptions.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums.Subscriptions;

namespace Test_Gigbridge_Backend.Application.Features.Subscriptions.Freelancer;

public sealed class SubscriptionDtoTests
{
    [Theory]
    [InlineData(30, "monthly")]
    [InlineData(365, "yearly")]
    public void PlanDto_ExposesBillingPeriod(int durationDays, string expected)
    {
        var plan = new SubscriptionPlanDto(
            Guid.NewGuid(), "Premium", null, 150m, "GigCoin",
            durationDays, null, 1);

        Assert.Equal(expected, plan.BillingPeriod);
    }

    [Fact]
    public void SubscriptionDto_DoesNotTreatFreePlanAsPremium()
    {
        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            SubscriptionsId = Guid.NewGuid(), SubscriptionPlansId = Guid.NewGuid(),
            UserId = Guid.NewGuid(), Status = SubscriptionStatus.Active,
            StartDate = now, EndDate = now.AddDays(30), CreatedAt = now,
            SubscriptionPlans = new SubscriptionPlan { Name = "Free", Price = 0m }
        };

        var result = SubscriptionDto.FromEntity(subscription, now);

        Assert.False(result.IsPremium);
    }

    [Fact]
    public void PlanDto_NormalizesLegacyVndPriceToGigCoin()
    {
        var plan = new SubscriptionPlan
        {
            SubscriptionPlansId = Guid.NewGuid(), Name = "Freelancer Pro",
            Price = 199_000m, Currency = "VND", DurationInDays = 30
        };

        var result = SubscriptionPlanDto.FromEntity(plan);

        Assert.Equal(199m, result.Price);
        Assert.Equal("GigCoin", result.Currency);
    }
}
