using Application.Common.Exceptions;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Services;

namespace Test_Gigbridge_backend.Application.Features.Admin.Analytics;

public sealed class AdminAnalyticsRangeResolverTests
{
    [Fact]
    public void Month_uses_Ict_calendar_boundaries_and_equal_prior_duration()
    {
        var result = AdminAnalyticsRangeResolver.Resolve(
            new AdminAnalyticsRangeRequest("month", new DateOnly(2026, 8, 20)),
            new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc), result.CurrentFromUtc);
        Assert.Equal(new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc), result.CurrentToUtc);
        Assert.Equal(result.CurrentToUtc - result.CurrentFromUtc,
            result.ComparisonToUtc - result.ComparisonFromUtc);
        Assert.Equal("Asia/Ho_Chi_Minh", result.TimeZone);
    }

    [Fact]
    public void Quarter_starts_on_calendar_quarter_in_Ict()
    {
        var result = AdminAnalyticsRangeResolver.Resolve(
            new AdminAnalyticsRangeRequest("quarter", new DateOnly(2026, 5, 10)), DateTime.UtcNow);

        Assert.Equal(new DateTime(2026, 3, 31, 17, 0, 0, DateTimeKind.Utc), result.CurrentFromUtc);
        Assert.Equal(new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc), result.CurrentToUtc);
    }

    [Fact]
    public void Custom_rejects_more_than_366_days()
    {
        Assert.Throws<BadRequestException>(() => AdminAnalyticsRangeResolver.Resolve(
            new AdminAnalyticsRangeRequest("custom", null, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 2)),
            DateTime.UtcNow));
    }
}
