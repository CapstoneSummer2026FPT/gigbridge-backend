using Domain.Enums.MarketplaceAnalytics;

namespace Domain.Entities;

public sealed class MarketplaceAnalyticsEvent
{
    public Guid MarketplaceAnalyticsEventId { get; set; }
    public MarketplaceAnalyticsEventType Type { get; set; }
    public string ActorKey { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public string? NormalizedQuery { get; set; }
    public Guid? JobPostId { get; set; }
    public Guid? SearchEventId { get; set; }
    public int? ResultCount { get; set; }
    public string? FilterMetadata { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public JobPost? JobPost { get; set; }
}
