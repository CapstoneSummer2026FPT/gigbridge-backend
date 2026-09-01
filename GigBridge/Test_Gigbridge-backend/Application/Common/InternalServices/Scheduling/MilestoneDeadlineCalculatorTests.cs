using Application.Common.InternalServices.Scheduling;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Scheduling;

public class MilestoneDeadlineCalculatorTests
{
    [Theory]
    [InlineData("3 days", 3)]
    [InlineData("1 day", 1)]
    [InlineData("2 weeks", 14)]
    [InlineData("1 month", 30)]
    [InlineData("1 year", 365)]
    [InlineData("2 tuần", 14)]
    [InlineData("1 ngày", 1)]
    public void TryParseWorkItemDurationDays_AcceptsValidFormats(string duration, int expectedDays)
    {
        var result = MilestoneDeadlineCalculator.TryParseWorkItemDurationDays(duration, out var days);

        Assert.True(result);
        Assert.Equal(expectedDays, days);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0 days")]
    [InlineData("-1 days")]
    [InlineData("abc")]
    [InlineData("3 fortnights")]
    public void TryParseWorkItemDurationDays_RejectsInvalidFormats(string? duration)
    {
        var result = MilestoneDeadlineCalculator.TryParseWorkItemDurationDays(duration, out var days);

        Assert.False(result);
        Assert.Equal(0, days);
    }

    [Fact]
    public void TryParseDurationDays_StillRejectsDayUnits_ForMilestoneLevelParsing()
    {
        var result = MilestoneDeadlineCalculator.TryParseDurationDays("3 days", out var days);

        Assert.False(result);
        Assert.Equal(0, days);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_ReturnsNoOverage_WhenSumIsWithinMilestoneDuration()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            "2 weeks",
            new[] { "3 days", "4 days" },
            out var totalDays,
            out var milestoneDays,
            out var overageDays);

        Assert.True(result);
        Assert.Equal(7, totalDays);
        Assert.Equal(14, milestoneDays);
        Assert.Equal(0, overageDays);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_ReturnsOverage_WhenSumExceedsMilestoneDuration()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            "1 week",
            new[] { "4 days", "4 days" },
            out var totalDays,
            out var milestoneDays,
            out var overageDays);

        Assert.True(result);
        Assert.Equal(8, totalDays);
        Assert.Equal(7, milestoneDays);
        Assert.Equal(1, overageDays);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_IsNoOp_WhenMilestoneDurationIsUnset()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            null,
            new[] { "10 days" },
            out _,
            out _,
            out var overageDays);

        Assert.False(result);
        Assert.Equal(0, overageDays);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_IsNoOp_WhenMilestoneDurationIsUnparseable()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            "3 days",
            new[] { "10 days" },
            out _,
            out _,
            out var overageDays);

        Assert.False(result);
        Assert.Equal(0, overageDays);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_TreatsUnparseableWorkItemDurationsAsZero()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            "1 week",
            new[] { "garbage", "3 days" },
            out var totalDays,
            out _,
            out var overageDays);

        Assert.True(result);
        Assert.Equal(3, totalDays);
        Assert.Equal(0, overageDays);
    }

    [Fact]
    public void TryGetWorkItemDurationOverage_ReturnsNoOverage_WhenWorkItemListIsEmpty()
    {
        var result = MilestoneDeadlineCalculator.TryGetWorkItemDurationOverage(
            "1 week",
            Array.Empty<string?>(),
            out var totalDays,
            out var milestoneDays,
            out var overageDays);

        Assert.True(result);
        Assert.Equal(0, totalDays);
        Assert.Equal(7, milestoneDays);
        Assert.Equal(0, overageDays);
    }
}
