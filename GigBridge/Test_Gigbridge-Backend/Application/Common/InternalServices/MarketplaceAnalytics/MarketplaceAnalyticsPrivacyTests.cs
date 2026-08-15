using Application.Common.InternalServices.MarketplaceAnalytics.Interfaces;
using Application.Common.InternalServices.MarketplaceAnalytics.Services;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.MarketplaceAnalytics;

public sealed class MarketplaceAnalyticsPrivacyTests
{
    [Theory]
    [InlineData("React developer", "react developer")]
    [InlineData("  UX   DESIGN  ", "ux design")]
    public void NormalizeSearch_keeps_stable_non_sensitive_queries(string input, string expected)
    {
        Assert.Equal(expected, MarketplaceAnalyticsRecorder.NormalizeSearch(input));
    }

    [Theory]
    [InlineData("person@example.com")]
    [InlineData("call +84 912 345 678")]
    [InlineData("https://example.com/portfolio")]
    [InlineData("a")]
    public void NormalizeSearch_rejects_sensitive_or_unstable_values(string input)
    {
        Assert.Null(MarketplaceAnalyticsRecorder.NormalizeSearch(input));
    }

    [Fact]
    public void NormalizeSearch_never_preserves_control_characters()
    {
        Assert.Null(MarketplaceAnalyticsRecorder.NormalizeSearch("react\u0000developer"));
    }
}
