namespace Application.Features.Admin.SystemTracking.Common.Models;

public sealed record SystemRequestLog(
    string Id,
    DateTimeOffset Timestamp,
    string Method,
    int StatusCode,
    string Path,
    long DurationMs,
    string RequestId);

public sealed record SystemErrorLog(
    string Id,
    DateTimeOffset Timestamp,
    string Level,
    string Service,
    string Message,
    string RequestId,
    int Count,
    string Source = "runtime",
    string? ExternalUrl = null,
    DateTimeOffset? FirstObservedAt = null,
    string? Status = null,
    string? Environment = null,
    string? Platform = null);

public sealed record SystemAlert(
    string Id,
    string Severity,
    string Title,
    string Description,
    string Metric,
    string Value,
    string Threshold,
    DateTimeOffset FirstObservedAt);

public sealed record SystemOverview(
    string Status,
    int TotalRequests,
    int ErrorRequests,
    double ErrorRatePercent,
    long AverageResponseMs,
    long P95ResponseMs,
    int ActiveAlerts);

public sealed record AiUsageBaseline(
    bool Configured,
    string Source,
    int TotalRequests,
    long InputTokens,
    long OutputTokens,
    decimal EstimatedCostUsd,
    IReadOnlyList<object> DailyUsage);

public sealed record ErrorMonitoringStatus(
    bool Configured,
    bool Available,
    string Provider,
    string Message);

public sealed record SystemErrorSourceResult(
    bool Configured,
    bool Available,
    string Provider,
    string Message,
    IReadOnlyList<SystemErrorLog> Errors);

public sealed record SystemTrackingSnapshot(
    DateTimeOffset GeneratedAt,
    string Environment,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    string RetentionMode,
    int RetainedEntryLimit,
    SystemOverview Overview,
    IReadOnlyList<SystemRequestLog> Requests,
    IReadOnlyList<SystemErrorLog> Errors,
    IReadOnlyList<SystemAlert> Alerts,
    AiUsageBaseline AiUsage,
    ErrorMonitoringStatus ErrorMonitoring);
