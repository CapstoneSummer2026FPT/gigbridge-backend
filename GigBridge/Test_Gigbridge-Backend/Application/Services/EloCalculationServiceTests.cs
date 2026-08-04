using Domain.Services;

namespace Test_Gigbridge_Backend.Application.Services;

public class EloCalculationServiceTests
{
    [Theory]
    [InlineData(1.0, -50)]
    [InlineData(1.5, -35)]
    [InlineData(2.0, -20)]
    [InlineData(2.5, -15)]
    [InlineData(3.0, -10)]
    [InlineData(3.2, -2)]
    [InlineData(3.3, 2)]
    [InlineData(3.5, 10)]
    [InlineData(4.0, 30)]
    [InlineData(4.5, 40)]
    [InlineData(5.0, 50)]
    public void CalculateEloChange_ReturnsReferenceTableValues(double rating, int expected)
    {
        Assert.Equal(expected, EloCalculationService.CalculateEloChange((decimal)rating));
    }

    [Theory]
    [InlineData(1.1, -47)]
    [InlineData(1.9, -23)]
    [InlineData(2.1, -19)]
    [InlineData(2.9, -11)]
    [InlineData(3.1, -6)]
    [InlineData(3.4, 6)]
    [InlineData(3.9, 26)]
    [InlineData(4.1, 32)]
    [InlineData(4.9, 48)]
    public void CalculateEloChange_InterpolatesLinearlyBetweenAnchors(double rating, int expected)
    {
        Assert.Equal(expected, EloCalculationService.CalculateEloChange((decimal)rating));
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(5.1)]
    [InlineData(3.35)]
    [InlineData(3.34)]
    [InlineData(1.05)]
    public void CalculateEloChange_RejectsInvalidRatings(double rating)
    {
        var value = (decimal)rating;
        Assert.False(EloCalculationService.IsValidRating(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => EloCalculationService.CalculateEloChange(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => EloCalculationService.EnsureValidRating(value));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.3)]
    [InlineData(4.0)]
    [InlineData(4.5)]
    [InlineData(5.0)]
    public void IsValidRating_AcceptsWholeAndOneDecimalRatings(double rating)
    {
        Assert.True(EloCalculationService.IsValidRating((decimal)rating));
    }
}
