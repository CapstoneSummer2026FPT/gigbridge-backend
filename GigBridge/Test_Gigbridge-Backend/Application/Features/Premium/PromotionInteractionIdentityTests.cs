using Application.Features.Premium.Common;

namespace Test_Gigbridge_backend.Application.Features.Premium;

public sealed class PromotionInteractionIdentityTests
{
    private static readonly Guid PromotionId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact]
    public void Repeated_interactions_in_the_same_window_have_the_same_identity()
    {
        var first = PromotionInteractionIdentityFactory.Create(
            "profile", PromotionId, "Impression", "visitor-1", Utc(0, 0, 1), 60);
        var repeated = PromotionInteractionIdentityFactory.Create(
            "profile", PromotionId, "Impression", "visitor-1", Utc(0, 0, 59), 60);

        Assert.Equal(first, repeated);
    }

    [Fact]
    public void A_later_window_gets_a_new_identity()
    {
        var first = PromotionInteractionIdentityFactory.Create(
            "job", PromotionId, "Click", "visitor-1", Utc(0, 0, 59), 60);
        var later = PromotionInteractionIdentityFactory.Create(
            "job", PromotionId, "Click", "visitor-1", Utc(0, 1, 0), 60);

        Assert.NotEqual(first.Key, later.Key);
        Assert.NotEqual(first.LockKey, later.LockKey);
    }

    private static DateTime Utc(int hour, int minute, int second) =>
        new(2026, 8, 1, hour, minute, second, DateTimeKind.Utc);
}
