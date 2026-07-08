using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class ProposalTotalsCalculatorTests
{
    [Fact]
    public void CalculateDuration_UsesLargestUnitAndRoundsUp()
    {
        Assert.Equal("20 days", ProposalTotalsCalculator.CalculateDuration(["5 days", "8 days", "7 days"]));
        Assert.Equal("4 weeks", ProposalTotalsCalculator.CalculateDuration(["3 weeks", "5 days"]));
        Assert.Equal("3 months", ProposalTotalsCalculator.CalculateDuration(["1 month", "6 weeks"]));
    }

    [Fact]
    public void ResolveValues_UsesMilestonesOnlyWhenRequestOmitsValues()
    {
        var milestones = new[]
        {
            new ProposalMilestonePlanDto { Amount = 100m, EstimatedDuration = "5 days" },
            new ProposalMilestonePlanDto { Amount = 250m, EstimatedDuration = "1 week" }
        };

        Assert.Equal(350m, ProposalTotalsCalculator.ResolveBudget(null, milestones));
        Assert.Equal("2 weeks", ProposalTotalsCalculator.ResolveDuration(null, milestones));
        Assert.Equal(999m, ProposalTotalsCalculator.ResolveBudget(999m, milestones));
        Assert.Equal("1 month", ProposalTotalsCalculator.ResolveDuration("1 month", milestones));
    }

    [Theory]
    [InlineData("1 day", true)]
    [InlineData("2 weeks", true)]
    [InlineData("1.5 weeks", false)]
    [InlineData("0 months", false)]
    public void IsValidDuration_RequiresPositiveWholeNumber(string value, bool expected)
    {
        Assert.Equal(expected, ProposalTotalsCalculator.IsValidDuration(value));
    }
}
