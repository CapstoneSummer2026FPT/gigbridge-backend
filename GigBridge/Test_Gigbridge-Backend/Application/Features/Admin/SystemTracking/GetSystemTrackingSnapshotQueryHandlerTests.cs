using Application.Features.Admin.SystemTracking.Common.Interfaces;
using Application.Features.Admin.SystemTracking.Common.Models;
using Application.Features.Admin.SystemTracking.GetSnapshot.Queries;

namespace Test_Gigbridge_Backend.Application.Features.Admin.SystemTracking;

public sealed class GetSystemTrackingSnapshotQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenNoExternalSourceExists_ReturnsRuntimeSnapshot()
    {
        var snapshot = CreateSnapshot();
        var handler = new GetSystemTrackingSnapshotQueryHandler(
            new StubTrackingReader(snapshot),
            []);

        var result = await handler.Handle(
            new GetSystemTrackingSnapshotQuery("Testing", 100),
            CancellationToken.None);

        Assert.Same(snapshot, result);
    }

    [Fact]
    public async Task Handle_WhenSentryIsAvailable_MergesNewestErrorsAndAddsAlert()
    {
        var runtimeError = Error("runtime", new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));
        var olderExternal = Error("external-old", new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero));
        var newerExternal = Error("external-new", new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero));
        var snapshot = CreateSnapshot([runtimeError]);
        var source = new StubErrorSource(new SystemErrorSourceResult(
            true,
            true,
            "sentry",
            "available",
            [olderExternal, newerExternal]));
        var handler = new GetSystemTrackingSnapshotQueryHandler(
            new StubTrackingReader(snapshot),
            [source]);

        var result = await handler.Handle(
            new GetSystemTrackingSnapshotQuery("Production", 2),
            CancellationToken.None);

        Assert.Equal(["external-new", "external-old"], result.Errors.Select(error => error.Id));
        var alert = Assert.Single(result.Alerts);
        Assert.Equal("sentry-unresolved-errors", alert.Id);
        Assert.Equal("sentry_unresolved_issues", alert.Metric);
        Assert.Equal("memory-current-instance+sentry-issues", result.RetentionMode);
        Assert.True(result.ErrorMonitoring.Configured);
        Assert.True(result.ErrorMonitoring.Available);
        Assert.Equal(1, result.Overview.ActiveAlerts);
    }

    [Fact]
    public async Task Handle_WhenConfiguredSourceIsUnavailable_PreservesRuntimeErrors()
    {
        var runtimeError = Error("runtime", DateTimeOffset.UtcNow);
        var snapshot = CreateSnapshot([runtimeError]);
        var source = new StubErrorSource(new SystemErrorSourceResult(
            true,
            false,
            "sentry",
            "Sentry unavailable.",
            []));
        var handler = new GetSystemTrackingSnapshotQueryHandler(
            new StubTrackingReader(snapshot),
            [source]);

        var result = await handler.Handle(
            new GetSystemTrackingSnapshotQuery("Production", 100),
            CancellationToken.None);

        Assert.Same(runtimeError, Assert.Single(result.Errors));
        Assert.Empty(result.Alerts);
        Assert.True(result.ErrorMonitoring.Configured);
        Assert.False(result.ErrorMonitoring.Available);
        Assert.Contains("Sentry unavailable.", result.ErrorMonitoring.Message);
    }

    [Fact]
    public async Task Handle_WhenSourceIsNotConfigured_ReportsDisabledMonitoring()
    {
        var source = new StubErrorSource(new SystemErrorSourceResult(
            false,
            false,
            "sentry",
            "Monitoring disabled.",
            []));
        var handler = new GetSystemTrackingSnapshotQueryHandler(
            new StubTrackingReader(CreateSnapshot()),
            [source]);

        var result = await handler.Handle(
            new GetSystemTrackingSnapshotQuery("Testing", 100),
            CancellationToken.None);

        Assert.False(result.ErrorMonitoring.Configured);
        Assert.False(result.ErrorMonitoring.Available);
        Assert.Equal("sentry", result.ErrorMonitoring.Provider);
        Assert.Equal("Monitoring disabled.", result.ErrorMonitoring.Message);
    }

    private static SystemTrackingSnapshot CreateSnapshot(
        IReadOnlyList<SystemErrorLog>? errors = null) =>
        new(
            DateTimeOffset.UtcNow,
            "Testing",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            60,
            "memory-current-instance",
            500,
            new SystemOverview("healthy", 0, 0, 0, 0, 0, 0),
            [],
            errors ?? [],
            [],
            new AiUsageBaseline(false, "not-connected", 0, 0, 0, 0, []),
            new ErrorMonitoringStatus(false, false, "sentry", "Not configured."));

    private static SystemErrorLog Error(string id, DateTimeOffset timestamp) =>
        new(id, timestamp, "error", "backend-api", id, id, 1);

    private sealed class StubTrackingReader(SystemTrackingSnapshot snapshot) : ISystemTrackingReader
    {
        public SystemTrackingSnapshot Snapshot(string environment, int requestedLimit) => snapshot;
    }

    private sealed class StubErrorSource(SystemErrorSourceResult result) : ISystemErrorSource
    {
        public Task<SystemErrorSourceResult> GetErrorsAsync(
            int requestedLimit,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
