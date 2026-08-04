using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace Test_Gigbridge_backend.Application.Features.Premium;

public sealed class PromotionPolicyTests
{
    [Fact]
    public void Defaults_CalculateConfiguredTargetAndWeight()
    {
        var policy = PromotionPolicy.Defaults;
        Assert.Equal(140, PromotionPolicy.TargetClicks(10m, policy));
        Assert.Equal(10m, PromotionPolicy.BoostWeight(10m, policy));
    }

    [Fact]
    public void Calculations_UseProvidedPolicyInsteadOfEmbeddedFormula()
    {
        var policy = new PromotionPolicyDto(25, 4, 2m, 1, 100, 80, 160, 120,
            1024, 2048, 64, 5, 20, 30, 7, 3);
        Assert.Equal(37, PromotionPolicy.TargetClicks(3m, policy));
        Assert.Equal(6m, PromotionPolicy.BoostWeight(3m, policy));
    }

    [Fact]
    public void QueueOrder_PrioritizesWeightAndPreservesPositionWithinWeight()
    {
        var now = DateTime.UtcNow;
        var lowerWeightFirst = Promotion(2m, 1, now.AddMinutes(-3));
        var higherWeightSecond = Promotion(5m, 2, now.AddMinutes(-2));
        var higherWeightLater = Promotion(5m, 5, now.AddMinutes(-1));
        var newHigherWeight = Promotion(5m, 0, now);

        var ordered = PromotionPolicy.OrderQueue(
            [lowerWeightFirst, higherWeightLater, newHigherWeight, higherWeightSecond],
            now);

        Guid[] expected =
        [
            higherWeightSecond.FreelancerProfilePromotionsId,
            higherWeightLater.FreelancerProfilePromotionsId,
            newHigherWeight.FreelancerProfilePromotionsId,
            lowerWeightFirst.FreelancerProfilePromotionsId
        ];
        Assert.Equal(expected,
            ordered.Select(item => item.FreelancerProfilePromotionsId));
    }

    private static FreelancerProfilePromotion Promotion(
        decimal weight,
        int queuePosition,
        DateTime createdAt) => new()
    {
        FreelancerProfilePromotionsId = Guid.NewGuid(),
        BoostWeight = weight,
        QueuePosition = queuePosition,
        CreatedAt = createdAt,
        StartTime = createdAt.AddMinutes(-1),
        EndTime = createdAt.AddDays(1),
        Status = PromotionStatus.Active
    };
}
