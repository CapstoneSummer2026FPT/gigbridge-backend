using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Services;
using Domain.Entities;
using Domain.Enums.MarketplaceAnalytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public sealed class AnalyticsMaintenanceWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsMaintenanceWorker> _logger;

    public AnalyticsMaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<AnalyticsMaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { _logger.LogError(exception, "Marketplace analytics maintenance failed."); }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var today = DateOnly.FromDateTime(clock.UtcNow);
        for (var offset = 1; offset <= 3; offset++)
            await RollupDay(context, clock.UtcNow, today.AddDays(-offset), cancellationToken);

        await context.Set<MarketplaceAnalyticsEvent>()
            .Where(x => x.OccurredAt < clock.UtcNow.AddMonths(-13))
            .ExecuteDeleteAsync(cancellationToken);
        await context.Set<MarketplaceAnalyticsDailyAggregate>()
            .Where(x => x.Date < today.AddMonths(-25))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task RollupDay(
        IApplicationDbContext context,
        DateTime utcNow,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var range = AdminAnalyticsRangeResolver.Resolve(new AdminAnalyticsRangeRequest("custom", null, date, date), utcNow);
        var events = await context.Set<MarketplaceAnalyticsEvent>().AsNoTracking()
            .Where(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc)
            .ToListAsync(cancellationToken);
        await context.Set<MarketplaceAnalyticsDailyAggregate>().Where(x => x.Date == date)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var group in events.Where(x => x.Type == MarketplaceAnalyticsEventType.Search && x.NormalizedQuery != null)
            .GroupBy(x => x.NormalizedQuery!, StringComparer.OrdinalIgnoreCase))
        {
            context.Set<MarketplaceAnalyticsDailyAggregate>().Add(new MarketplaceAnalyticsDailyAggregate
            {
                MarketplaceAnalyticsDailyAggregateId = Guid.NewGuid(), Date = date,
                DimensionType = "query", DimensionKey = group.Key, Label = group.Key,
                SearchCount = group.LongCount(), DistinctActorCount = group.Select(x => x.ActorKey).Distinct().LongCount(),
                ZeroResultCount = group.LongCount(x => x.ResultCount == 0),
                ResultCountTotal = group.Sum(x => (long)(x.ResultCount ?? 0)), UpdatedAt = utcNow
            });
        }
        foreach (var group in events.Where(x => x.JobPostId != null).GroupBy(x => x.JobPostId!.Value))
        {
            context.Set<MarketplaceAnalyticsDailyAggregate>().Add(new MarketplaceAnalyticsDailyAggregate
            {
                MarketplaceAnalyticsDailyAggregateId = Guid.NewGuid(), Date = date,
                DimensionType = "job", DimensionKey = group.Key.ToString("N"), Label = group.Key.ToString(),
                DistinctActorCount = group.Select(x => x.ActorKey).Distinct().LongCount(),
                ViewCount = group.LongCount(x => x.Type == MarketplaceAnalyticsEventType.JobOpen),
                SaveCount = group.LongCount(x => x.Type == MarketplaceAnalyticsEventType.JobSave), UpdatedAt = utcNow
            });
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
