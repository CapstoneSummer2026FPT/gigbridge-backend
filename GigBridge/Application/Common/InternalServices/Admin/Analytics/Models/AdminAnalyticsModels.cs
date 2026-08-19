using Domain.Enums.Wallets;

namespace Application.Common.InternalServices.Admin.Analytics.Models;
public sealed record AdminAnalyticsRangeRequest(
    string Period = "month",
    DateOnly? Anchor = null,
    DateOnly? From = null,
    DateOnly? To = null);

public sealed record ResolvedAdminAnalyticsRange(
    string Period,
    DateTime CurrentFromUtc,
    DateTime CurrentToUtc,
    DateTime ComparisonFromUtc,
    DateTime ComparisonToUtc,
    string TimeZone,
    string BucketGranularity);

public sealed record AnalyticsAvailability(
    DateTime? CollectionStartedAt,
    DateTime? BackfillCompletedAt,
    bool IsPartial,
    string? Note);

public sealed record AnalyticsResponseMeta(
    ResolvedAdminAnalyticsRange Range,
    DateTime GeneratedAt,
    AnalyticsAvailability Availability,
    long ClassifiedSourceCount,
    long UnclassifiedRetainedLookingRows);

public sealed record AnalyticsKpi(
    string Key,
    decimal Value,
    decimal ComparisonValue,
    decimal? ChangePercent,
    string Unit);

public sealed record AnalyticsSeriesPoint(DateOnly Bucket, string Series, decimal Value);
public sealed record AnalyticsBreakdown(string Key, string Label, decimal Value, long Count);

public sealed record FinanceAnalyticsResponse(
    AnalyticsResponseMeta Meta,
    IReadOnlyList<AnalyticsKpi> Kpis,
    IReadOnlyList<AnalyticsBreakdown> RevenueSources,
    IReadOnlyList<AnalyticsSeriesPoint> RevenueSeries,
    IReadOnlyList<AnalyticsSeriesPoint> GmvSeries,
    IReadOnlyList<AnalyticsSeriesPoint> CashFlowSeries,
    decimal TopUpInflowVnd,
    decimal WithdrawalPayoutVnd,
    long EscrowReleaseCount);

public sealed record PremiumPlanBreakdown(
    string Plan,
    string Role,
    long Purchases,
    decimal RevenueGigCoin,
    decimal RevenueVnd);

public sealed record PremiumFeatureMetric(
    string Feature,
    long Events,
    long DistinctUsers,
    decimal? ClickThroughRate);

public sealed record PremiumPromotionSummary(
    string Type,
    string Role,
    long Total,
    long Active,
    decimal TokenSpend,
    long Impressions,
    long Clicks,
    decimal ClickThroughRate);

public sealed record PremiumPromotionRecord(
    Guid PromotionId,
    string Type,
    string Role,
    Guid OwnerUserId,
    string OwnerName,
    string OwnerEmail,
    Guid SubjectId,
    string SubjectName,
    string Status,
    decimal TokenCost,
    int ImpressionCount,
    int ClickCount,
    decimal ClickThroughRate,
    DateTime StartsAt,
    DateTime EndsAt,
    DateTime CreatedAt,
    IReadOnlyDictionary<string, string?> Attributes);

public sealed record PremiumAnalyticsResponse(
    AnalyticsResponseMeta Meta,
    IReadOnlyList<AnalyticsKpi> Kpis,
    IReadOnlyList<PremiumPlanBreakdown> Plans,
    IReadOnlyList<PremiumFeatureMetric> FeatureAdoption,
    long NewPurchases,
    long Renewals,
    long Cancellations,
    long HistoricalPromotionImpressions,
    long HistoricalPromotionClicks,
    IReadOnlyList<PremiumPromotionSummary> PromotionSummaries,
    IReadOnlyList<PremiumPromotionRecord> Promotions,
    long PromotionRecordCount,
    bool PromotionsTruncated);

public sealed record AdminTransactionFilter(
    AdminAnalyticsRangeRequest Range,
    Guid? UserId = null,
    Guid? ContractId = null,
    int? Type = null,
    int? Status = null,
    string? Gateway = null,
    PlatformRevenueSource? RevenueSource = null,
    string? Cursor = null,
    int PageSize = 50);

public sealed record AdminTransactionItem(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string UserName,
    Guid? ContractId,
    string? ContractTitle,
    int Type,
    string TypeLabel,
    int Status,
    string StatusLabel,
    string Direction,
    decimal GigCoinAmount,
    decimal VndAmount,
    string? Gateway,
    string? Reference,
    string? Note,
    string? Metadata,
    string? RevenueSource);

public sealed record AdminTransactionPage(
    AnalyticsResponseMeta Meta,
    IReadOnlyList<AdminTransactionItem> Items,
    string? NextCursor,
    int PageSize,
    long FilteredCount,
    IReadOnlyList<AnalyticsBreakdown> TypeBreakdown,
    IReadOnlyList<AnalyticsBreakdown> StatusBreakdown,
    IReadOnlyList<AnalyticsSeriesPoint> CountSeries);

public sealed record MarketplaceSearchMetric(
    string Query,
    long Searches,
    long DistinctActors,
    long ZeroResultSearches,
    decimal AverageResultCount,
    decimal OpportunityScore);

public sealed record TrendingJobMetric(
    Guid JobPostId,
    string Title,
    decimal Score,
    long UniqueViews,
    long Saves,
    long Proposals,
    long Contracts,
    decimal ConversionPercent,
    IReadOnlyList<long> Sparkline);

public sealed record SupplyGapMetric(
    string Kind,
    string Key,
    string Label,
    decimal Score,
    long Demand,
    long Supply,
    long ResultCount,
    long ProposalCount,
    long ContractCount);

public sealed record MarketplaceFunnel(long Views, long Saves, long Proposals, long Contracts);

public sealed record MarketplaceAnalyticsResponse(
    AnalyticsResponseMeta Meta,
    IReadOnlyList<MarketplaceSearchMetric> TopSearches,
    IReadOnlyList<TrendingJobMetric> TrendingJobs,
    MarketplaceFunnel Funnel,
    IReadOnlyList<SupplyGapMetric> Opportunities);
