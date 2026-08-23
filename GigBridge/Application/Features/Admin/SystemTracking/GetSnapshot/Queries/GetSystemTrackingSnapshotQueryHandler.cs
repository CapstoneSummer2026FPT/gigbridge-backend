using Application.Common.InternalServices.Admin.SystemTracking.Models;
using Application.Common.InternalServices.Admin.SystemTracking.Interfaces;
using MediatR;

namespace Application.Features.Admin.SystemTracking.GetSnapshot.Queries;

public sealed class GetSystemTrackingSnapshotQueryHandler(
    ISystemTrackingReader reader,
    IEnumerable<ISystemErrorSource> errorSources)
    : IRequestHandler<GetSystemTrackingSnapshotQuery, SystemTrackingSnapshot>
{
    public async Task<SystemTrackingSnapshot> Handle(
        GetSystemTrackingSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = reader.Snapshot(request.Environment, request.Limit);
        var sources = errorSources.ToArray();
        if (sources.Length == 0)
        {
            return snapshot;
        }

        var results = await Task.WhenAll(
            sources.Select(source => source.GetErrorsAsync(request.Limit, cancellationToken)));
        var configuredResults = results.Where(result => result.Configured).ToArray();
        var availableResults = configuredResults.Where(result => result.Available).ToArray();
        var externalErrors = availableResults
            .SelectMany(result => result.Errors)
            .OrderByDescending(error => error.Timestamp)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .ToArray();
        var errors = externalErrors
            .Concat(snapshot.Errors)
            .OrderByDescending(error => error.Timestamp)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .ToArray();

        var monitoring = configuredResults.Length == 0
            ? results.FirstOrDefault() is { } disabled
                ? new ErrorMonitoringStatus(false, false, disabled.Provider, disabled.Message)
                : snapshot.ErrorMonitoring
            : new ErrorMonitoringStatus(
                true,
                availableResults.Length > 0,
                string.Join(", ", configuredResults.Select(result => result.Provider).Distinct()),
                availableResults.Length > 0
                    ? $"Loaded {externalErrors.Length} grouped production issue(s)."
                    : string.Join(" ", configuredResults.Select(result => result.Message)));

        var alerts = snapshot.Alerts.ToList();
        if (externalErrors.Length > 0)
        {
            alerts.Add(new SystemAlert(
                "sentry-unresolved-errors",
                externalErrors.Any(error => error.Level == "critical") ? "critical" : "warning",
                "Unresolved application errors",
                $"Sentry reports {externalErrors.Length} unresolved issue group(s).",
                "sentry_unresolved_issues",
                externalErrors.Length.ToString(),
                "0",
                externalErrors.Min(error => error.FirstObservedAt ?? error.Timestamp)));
        }

        return snapshot with
        {
            RetentionMode = configuredResults.Length > 0
                ? $"{snapshot.RetentionMode}+sentry-issues"
                : snapshot.RetentionMode,
            Errors = errors,
            Alerts = alerts,
            Overview = snapshot.Overview with { ActiveAlerts = alerts.Count },
            ErrorMonitoring = monitoring
        };
    }
}
