namespace Domain.Entities;

public sealed class MarketplaceAnalyticsDailyAggregate
{
    public Guid MarketplaceAnalyticsDailyAggregateId { get; set; }
    public DateOnly Date { get; set; }
    public string DimensionType { get; set; } = string.Empty;
    public string DimensionKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long SearchCount { get; set; }
    public long DistinctActorCount { get; set; }
    public long ZeroResultCount { get; set; }
    public long ResultCountTotal { get; set; }
    public long ViewCount { get; set; }
    public long SaveCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
