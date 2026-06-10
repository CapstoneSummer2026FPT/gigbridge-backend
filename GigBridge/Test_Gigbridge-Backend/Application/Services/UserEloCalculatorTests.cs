using Domain.Services;

namespace Test_Gigbridge_Backend;

public class UserEloCalculatorTests
{
    [Theory]
    [InlineData(1, -50)]
    [InlineData(2, -30)]
    [InlineData(3, 40)]
    [InlineData(4, 60)]
    [InlineData(5, 70)]
    public void CalculateReviewEventDelta_ReturnsConfiguredDelta(int rating, int expectedDelta)
    {
        var delta = UserEloCalculator.CalculateReviewEventDelta(rating);

        Assert.Equal(expectedDelta, delta);
    }

    [Fact]
    public void ApplyDelta_ClampsPointsAtZero()
    {
        var points = UserEloCalculator.ApplyDelta(20, -50);

        Assert.Equal(0, points);
    }

    [Theory]
    [InlineData(13, 0)]
    [InlineData(14, -50)]
    [InlineData(28, -100)]
    public void CalculateInactivityPenalty_UsesFullFourteenDayPeriods(int inactiveDays, int expectedDelta)
    {
        var lastActivityAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = lastActivityAt.AddDays(inactiveDays);

        var penalty = UserEloCalculator.CalculateInactivityPenalty(lastActivityAt, now);

        Assert.Equal(expectedDelta, penalty);
    }

    [Theory]
    [InlineData(13, 0)]
    [InlineData(14, 100)]
    [InlineData(28, 100)]
    public void CalculateReturnBonus_AppliesOnceForInactiveReturn(int inactiveDays, int expectedDelta)
    {
        var lastActivityAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = lastActivityAt.AddDays(inactiveDays);

        var bonus = UserEloCalculator.CalculateReturnBonus(lastActivityAt, now);

        Assert.Equal(expectedDelta, bonus);
    }
}
