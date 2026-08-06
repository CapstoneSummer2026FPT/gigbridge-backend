namespace Application.Features.Admin.Dashboard.Common.DTOs;

public sealed record AdminDashboardRange(
    int Days,
    DateTime CurrentFromUtc,
    DateTime CurrentToUtc,
    DateTime ComparisonFromUtc,
    DateTime ComparisonToUtc,
    string TimeZone);

public sealed record AdminDashboardCountMetric(
    long Value,
    long PeriodValue,
    long ComparisonValue,
    decimal? ChangePercent);

public sealed record AdminDashboardMoneyMetric(
    decimal Value,
    decimal ComparisonValue,
    decimal? ChangePercent,
    string Unit);

public sealed record AdminDashboardActivityPoint(
    DateOnly Bucket,
    long Users,
    long JobPosts,
    long Proposals,
    long Contracts);

public sealed record AdminDashboardWorkQueue(
    long Reports,
    long ContractReports,
    long Disputes,
    long Withdrawals);

public sealed record AdminDashboardSummary(
    DateTime GeneratedAt,
    AdminDashboardRange Range,
    AdminDashboardCountMetric MarketplaceUsers,
    AdminDashboardCountMetric OpenJobPosts,
    AdminDashboardCountMetric ActiveContracts,
    AdminDashboardMoneyMetric MarketplaceGmv,
    IReadOnlyList<AdminDashboardActivityPoint> Activity,
    AdminDashboardWorkQueue WorkQueue);
