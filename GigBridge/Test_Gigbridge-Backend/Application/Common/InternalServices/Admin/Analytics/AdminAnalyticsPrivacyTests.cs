using Application.Common.InternalServices.Admin.Analytics.Services;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Admin.Analytics;

public sealed class AdminAnalyticsPrivacyTests
{
    [Fact]
    public void Daily_actor_counts_are_not_summed_across_days()
    {
        var actors = AdminAnalyticsService.ConservativeDistinctActorCount(new long[] { 1, 1, 1 });

        Assert.Equal(1, actors);
    }

    [Fact]
    public void A_single_day_meeting_the_threshold_remains_disclosable()
    {
        var actors = AdminAnalyticsService.ConservativeDistinctActorCount(new long[] { 1, 3, 2 });

        Assert.Equal(3, actors);
    }
}
