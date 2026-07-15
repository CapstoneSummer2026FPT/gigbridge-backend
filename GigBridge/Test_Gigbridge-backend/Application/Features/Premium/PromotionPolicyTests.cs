using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;

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
}
