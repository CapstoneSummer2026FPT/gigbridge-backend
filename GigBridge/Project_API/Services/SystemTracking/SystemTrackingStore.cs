using System.Diagnostics;
using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Application.Features.Admin.SystemTracking.Common.Models;

namespace Project_API.Services.SystemTracking;

/// <summary>
/// A bounded, process-local telemetry store. It is intentionally dependency-free so
/// local development and a single deployed API instance expose the same contract.
/// Persistent, cross-replica telemetry belongs to the Phase 2 observability provider.
/// </summary>
public sealed class SystemTrackingStore : ISystemTrackingReader
{
    private const int EntryLimit = 500;
    private const long SlowRequestThresholdMs = 2_000;
    private readonly object _gate = new();
    private readonly List<SystemRequestLog> _requests = [];
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public void Record(HttpContext context, long durationMs, int statusCode)
    {
        if (context.Request.Path.StartsWithSegments("/api/admin/system-tracking"))
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var requestId = Activity.Current?.Id ?? context.TraceIdentifier;
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";

        string user = "Guest";
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            user = context.User.Identity.Name 
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? context.User.FindFirst("email")?.Value
                ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value
                ?? "Authenticated User";
        }

        string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "127.0.0.1";

        if (ip == "::1")
        {
            ip = "127.0.0.1";
        }

        var entry = new SystemRequestLog(
            requestId,
            timestamp,
            context.Request.Method,
            statusCode,
            path,
            durationMs,
            requestId,
            user,
            ip);

        lock (_gate)
        {
            _requests.Add(entry);
            var overflow = _requests.Count - EntryLimit;
            if (overflow > 0)
            {
                _requests.RemoveRange(0, overflow);
            }
        }
    }

    public SystemTrackingSnapshot Snapshot(string environment, int requestedLimit)
    {
        var limit = Math.Clamp(requestedLimit, 1, 200);
        List<SystemRequestLog> allRequests;
        lock (_gate)
        {
            allRequests = [.. _requests];
        }

        var recentRequests = allRequests
            .OrderByDescending(item => item.Timestamp)
            .Take(limit)
            .ToArray();
        var errorRequests = allRequests.Where(item => item.StatusCode >= 500).ToArray();
        var durations = allRequests.Select(item => item.DurationMs).OrderBy(value => value).ToArray();
        var average = durations.Length == 0 ? 0 : (long)Math.Round(durations.Average());
        var p95Index = durations.Length == 0
            ? 0
            : Math.Min(durations.Length - 1, (int)Math.Ceiling(durations.Length * 0.95) - 1);
        var p95 = durations.Length == 0 ? 0 : durations[p95Index];
        var errorRate = allRequests.Count == 0
            ? 0
            : Math.Round(errorRequests.Length * 100d / allRequests.Count, 2);

        var errors = errorRequests
            .OrderByDescending(item => item.Timestamp)
            .Take(limit)
            .Select(item => new SystemErrorLog(
                item.Id,
                item.Timestamp,
                item.StatusCode >= 500 ? "error" : "warning",
                "backend-api",
                $"{item.Method} {item.Path} responded with HTTP {item.StatusCode}",
                item.RequestId,
                1))
            .ToArray();

        var alerts = BuildAlerts(allRequests, errorRequests, p95);
        var status = errorRequests.Length > 0 || p95 >= SlowRequestThresholdMs
            ? "degraded"
            : "healthy";

        return new SystemTrackingSnapshot(
            DateTimeOffset.UtcNow,
            environment,
            _startedAt,
            (long)(DateTimeOffset.UtcNow - _startedAt).TotalSeconds,
            "memory-current-instance",
            EntryLimit,
            new SystemOverview(
                status,
                allRequests.Count,
                errorRequests.Length,
                errorRate,
                average,
                p95,
                alerts.Count),
            recentRequests,
            errors,
            alerts,
            new AiUsageBaseline(
                false,
                "not-connected",
                0,
                0,
                0,
                0,
                []),
            new ErrorMonitoringStatus(
                false,
                false,
                "sentry",
                "Sentry issue monitoring is not configured."));
    }

    private static IReadOnlyList<SystemAlert> BuildAlerts(
        IReadOnlyList<SystemRequestLog> allRequests,
        IReadOnlyList<SystemRequestLog> errors,
        long p95)
    {
        var alerts = new List<SystemAlert>();
        var now = DateTimeOffset.UtcNow;
        var recentErrors = errors.Where(item => item.Timestamp >= now.AddMinutes(-15)).ToArray();

        if (recentErrors.Length > 0)
        {
            alerts.Add(new SystemAlert(
                "backend-5xx",
                "critical",
                "Backend errors detected",
                $"{recentErrors.Length} server error(s) were observed in the last 15 minutes.",
                "http_5xx_15m",
                recentErrors.Length.ToString(),
                "0",
                recentErrors.Min(item => item.Timestamp)));
        }

        if (allRequests.Count >= 10)
        {
            var errorRate = errors.Count * 100d / allRequests.Count;
            if (errorRate >= 5)
            {
                alerts.Add(new SystemAlert(
                    "backend-error-rate",
                    "warning",
                    "Elevated backend error rate",
                    "The retained request window has crossed the 5% server-error threshold.",
                    "http_5xx_rate",
                    $"{errorRate:F1}%",
                    "< 5%",
                    errors.Count > 0 ? errors.Min(item => item.Timestamp) : now));
            }
        }

        if (p95 >= SlowRequestThresholdMs)
        {
            alerts.Add(new SystemAlert(
                "backend-latency-p95",
                "warning",
                "High API latency",
                "The retained request window has a slow p95 response time.",
                "http_duration_p95",
                $"{p95} ms",
                $"< {SlowRequestThresholdMs} ms",
                now));
        }

        return alerts;
    }
}
