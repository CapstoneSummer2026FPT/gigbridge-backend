using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Analytics.Common.Services;

public sealed class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public AdminAnalyticsService(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<FinanceAnalyticsResponse> GetFinanceAsync(
        AdminAnalyticsRangeRequest request,
        CancellationToken cancellationToken)
    {
        var range = AdminAnalyticsRangeResolver.Resolve(request, _clock.UtcNow);
        var current = await RevenueInRange(range.CurrentFromUtc, range.CurrentToUtc, cancellationToken);
        var comparison = await RevenueInRange(range.ComparisonFromUtc, range.ComparisonToUtc, cancellationToken);
        var releases = await _context.Set<EscrowTransaction>().AsNoTracking()
            .Where(x => x.Type == (int)EscrowTransactionType.ReleaseToFreelancer &&
                        x.Status == (int)EscrowTransactionStatus.Succeeded &&
                        (x.CompletedAt ?? x.CreatedAt) >= range.CurrentFromUtc &&
                        (x.CompletedAt ?? x.CreatedAt) < range.CurrentToUtc)
            .Select(x => new { x.Amount, At = x.CompletedAt ?? x.CreatedAt })
            .ToListAsync(cancellationToken);
        var comparisonGmv = await _context.Set<EscrowTransaction>().AsNoTracking()
            .Where(x => x.Type == (int)EscrowTransactionType.ReleaseToFreelancer &&
                        x.Status == (int)EscrowTransactionStatus.Succeeded &&
                        (x.CompletedAt ?? x.CreatedAt) >= range.ComparisonFromUtc &&
                        (x.CompletedAt ?? x.CreatedAt) < range.ComparisonToUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var topUps = await _context.Set<WalletTransaction>().AsNoTracking()
            .Where(x => x.Type == (int)WalletTransactionType.TopUp && x.Status == (int)WalletTransactionStatus.Succeeded &&
                        (x.CompletedAt ?? x.CreatedAt) >= range.CurrentFromUtc && (x.CompletedAt ?? x.CreatedAt) < range.CurrentToUtc)
            .Select(x => new { x.VndAmount, At = x.CompletedAt ?? x.CreatedAt }).ToListAsync(cancellationToken);
        var withdrawals = await _context.Set<WalletWithdrawal>().AsNoTracking()
            .Where(x => x.Status == (int)WithdrawalStatus.Success &&
                        (x.CompletedAt ?? x.CreatedAt) >= range.CurrentFromUtc && (x.CompletedAt ?? x.CreatedAt) < range.CurrentToUtc)
            .Select(x => new { x.NetVndAmount, At = x.CompletedAt ?? x.CreatedAt }).ToListAsync(cancellationToken);
        var comparisonTopUps = await SumTopUps(range.ComparisonFromUtc, range.ComparisonToUtc, cancellationToken);
        var comparisonWithdrawals = await SumWithdrawals(range.ComparisonFromUtc, range.ComparisonToUtc, cancellationToken);

        var revenue = current.Sum(x => x.VndEquivalent);
        var previousRevenue = comparison.Sum(x => x.VndEquivalent);
        var gmv = releases.Sum(x => x.Amount);
        var contractFees = current.Where(x => x.Source is PlatformRevenueSource.ContractFundingFee or PlatformRevenueSource.ContractReleaseFee)
            .Sum(x => x.VndEquivalent);
        var previousContractFees = comparison.Where(x => x.Source is PlatformRevenueSource.ContractFundingFee or PlatformRevenueSource.ContractReleaseFee)
            .Sum(x => x.VndEquivalent);
        var topUpTotal = topUps.Sum(x => x.VndAmount);
        var withdrawalTotal = withdrawals.Sum(x => x.NetVndAmount);
        var cash = topUpTotal - withdrawalTotal;
        var previousCash = comparisonTopUps - comparisonWithdrawals;

        var kpis = new List<AnalyticsKpi>
        {
            Kpi("grossRevenue", revenue, previousRevenue, "VND"),
            Kpi("revenueGrowth", Growth(revenue, previousRevenue) ?? 0m, 0m, "percent", false),
            Kpi("contractTakeRate", gmv == 0 ? 0 : contractFees / gmv * 100m,
                comparisonGmv == 0 ? 0 : previousContractFees / comparisonGmv * 100m, "percent"),
            Kpi("marketplaceGmv", gmv, comparisonGmv, "VND"),
            Kpi("netCashMovement", cash, previousCash, "VND")
        };

        var sources = current.GroupBy(x => x.Source).Select(group => new AnalyticsBreakdown(
            group.Key.ToString(), Humanize(group.Key.ToString()), group.Sum(x => x.VndEquivalent), group.LongCount()))
            .OrderByDescending(x => x.Value).ToList();
        var revenueSeries = current.GroupBy(x => new { Bucket = AdminAnalyticsRangeResolver.Bucket(x.OccurredAt, range), x.Source })
            .Select(x => new AnalyticsSeriesPoint(x.Key.Bucket, x.Key.Source.ToString(), x.Sum(y => y.VndEquivalent)))
            .OrderBy(x => x.Bucket).ThenBy(x => x.Series).ToList();
        var gmvSeries = releases.GroupBy(x => AdminAnalyticsRangeResolver.Bucket(x.At, range))
            .Select(x => new AnalyticsSeriesPoint(x.Key, "MarketplaceGMV", x.Sum(y => y.Amount))).OrderBy(x => x.Bucket).ToList();
        var cashSeries = topUps.GroupBy(x => AdminAnalyticsRangeResolver.Bucket(x.At, range))
                .Select(x => new AnalyticsSeriesPoint(x.Key, "TopUpInflow", x.Sum(y => y.VndAmount)))
            .Concat(withdrawals.GroupBy(x => AdminAnalyticsRangeResolver.Bucket(x.At, range))
                .Select(x => new AnalyticsSeriesPoint(x.Key, "WithdrawalPayout", -x.Sum(y => y.NetVndAmount))))
            .OrderBy(x => x.Bucket).ThenBy(x => x.Series).ToList();

        return new FinanceAnalyticsResponse(
            await BuildMeta(range, cancellationToken), kpis, sources, revenueSeries, gmvSeries, cashSeries,
            topUpTotal, withdrawalTotal, releases.LongCount());
    }

    public async Task<PremiumAnalyticsResponse> GetPremiumAsync(
        AdminAnalyticsRangeRequest request,
        CancellationToken cancellationToken)
    {
        var range = AdminAnalyticsRangeResolver.Resolve(request, _clock.UtcNow);
        var subscriptionVnd = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .Where(x => x.Source == PlatformRevenueSource.SubscriptionPurchase &&
                        x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc)
            .SumAsync(x => (decimal?)x.VndEquivalent, cancellationToken) ?? 0m;
        var previousSubscriptionVnd = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .Where(x => x.Source == PlatformRevenueSource.SubscriptionPurchase &&
                        x.OccurredAt >= range.ComparisonFromUtc && x.OccurredAt < range.ComparisonToUtc)
            .SumAsync(x => (decimal?)x.VndEquivalent, cancellationToken) ?? 0m;
        var subscriptions = await _context.Set<Subscription>().AsNoTracking()
            .Include(x => x.SubscriptionPlans)
            .Where(x => x.CreatedAt >= range.CurrentFromUtc && x.CreatedAt < range.CurrentToUtc)
            .ToListAsync(cancellationToken);
        var cancellations = await _context.Set<Subscription>().AsNoTracking()
            .LongCountAsync(x => x.CancelledAt != null &&
                                 x.CancelledAt >= range.CurrentFromUtc &&
                                 x.CancelledAt < range.CurrentToUtc, cancellationToken);
        // Cancelling a subscription disables renewal; paid access remains valid through EndDate.
        // Immediate administrative revocation truncates EndDate to the revocation time.
        var activePaidUsers = await _context.Set<Subscription>().AsNoTracking()
            .Where(x => x.StartDate < range.CurrentToUtc && x.EndDate >= range.CurrentToUtc &&
                        x.SubscriptionPlans.Price > 0)
            .Select(x => x.UserId).Distinct().CountAsync(cancellationToken);
        var previousActive = await _context.Set<Subscription>().AsNoTracking()
            .Where(x => x.StartDate < range.ComparisonToUtc && x.EndDate >= range.ComparisonToUtc &&
                        x.SubscriptionPlans.Price > 0)
            .Select(x => x.UserId).Distinct().CountAsync(cancellationToken);
        var usage = await _context.Set<PremiumUsageEvent>().AsNoTracking()
            .Where(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc)
            .ToListAsync(cancellationToken);
        var promotionSources = new[]
        {
            PlatformRevenueSource.JobPromotionPurchase,
            PlatformRevenueSource.ProfilePromotionPurchase,
            PlatformRevenueSource.PromotionBoost
        };
        var promotionRevenueVnd = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .Where(x => promotionSources.Contains(x.Source) &&
                        x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc)
            .SumAsync(x => (decimal?)x.VndEquivalent, cancellationToken) ?? 0m;
        var previousPromotionRevenueVnd = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .Where(x => promotionSources.Contains(x.Source) &&
                        x.OccurredAt >= range.ComparisonFromUtc && x.OccurredAt < range.ComparisonToUtc)
            .SumAsync(x => (decimal?)x.VndEquivalent, cancellationToken) ?? 0m;

        var earlierSubscriptionUsers = await _context.Set<Subscription>().AsNoTracking()
            .Where(x => x.CreatedAt < range.CurrentFromUtc).Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        var earlier = earlierSubscriptionUsers.ToHashSet();
        var planRows = subscriptions.GroupBy(x => new
            {
                Plan = x.SubscriptionPlans.Name,
                Role = x.SubscriptionPlans.TargetRole == null ? "All" : ((UserRole)x.SubscriptionPlans.TargetRole).ToString()
            })
            .Select(group => new PremiumPlanBreakdown(
                group.Key.Plan, group.Key.Role, group.LongCount(),
                group.Sum(x => x.SubscriptionPlans.Currency == "GigCoin" ? x.SubscriptionPlans.Price : x.SubscriptionPlans.Price / 1000m),
                group.Sum(x => x.SubscriptionPlans.Currency == "VND" ? x.SubscriptionPlans.Price : x.SubscriptionPlans.Price * 1000m)))
            .OrderByDescending(x => x.Purchases).ToList();
        var featureRows = usage.GroupBy(x => x.Type).Select(group => new PremiumFeatureMetric(
            group.Key.ToString(), group.LongCount(), group.Where(x => x.UserId != null).Select(x => x.UserId).Distinct().LongCount(), null))
            .OrderByDescending(x => x.Events).ToList();
        var datedImpressions = usage.LongCount(x => x.Type == PremiumUsageEventType.PromotionImpression);
        var datedClicks = usage.LongCount(x => x.Type == PremiumUsageEventType.PromotionClick);
        if (datedImpressions > 0)
            featureRows.Add(new PremiumFeatureMetric("PromotionCTR", datedImpressions, 0, datedClicks * 100m / datedImpressions));
        var historicalJobImpressions = await _context.Set<JobPostPromotion>().AsNoTracking().SumAsync(x => (long?)x.ImpressionCount, cancellationToken) ?? 0;
        var historicalJobClicks = await _context.Set<JobPostPromotion>().AsNoTracking().SumAsync(x => (long?)x.ClickCount, cancellationToken) ?? 0;
        var historicalProfileImpressions = await _context.Set<FreelancerProfilePromotion>().AsNoTracking().SumAsync(x => (long?)x.ImpressionCount, cancellationToken) ?? 0;
        var historicalProfileClicks = await _context.Set<FreelancerProfilePromotion>().AsNoTracking().SumAsync(x => (long?)x.ClickCount, cancellationToken) ?? 0;

        var kpis = new List<AnalyticsKpi>
        {
            Kpi("premiumRevenue", subscriptionVnd + promotionRevenueVnd,
                previousSubscriptionVnd + previousPromotionRevenueVnd, "VND"),
            Kpi("activePaidUsers", activePaidUsers, previousActive, "users"),
            Kpi("paidFeatureUsers", usage.Where(x => x.UserId != null).Select(x => x.UserId).Distinct().Count(), 0, "users"),
            Kpi("promotionCtr", datedImpressions == 0 ? 0 : datedClicks * 100m / datedImpressions, 0, "percent")
        };
        return new PremiumAnalyticsResponse(
            await BuildMeta(range, cancellationToken), kpis, planRows, featureRows,
            subscriptions.LongCount(), subscriptions.LongCount(x => earlier.Contains(x.UserId)),
            cancellations, historicalJobImpressions + historicalProfileImpressions,
            historicalJobClicks + historicalProfileClicks);
    }

    public async Task<AdminTransactionPage> GetTransactionsAsync(
        AdminTransactionFilter filter,
        CancellationToken cancellationToken)
    {
        var range = AdminAnalyticsRangeResolver.Resolve(filter.Range, _clock.UtcNow);
        var query = FilterTransactions(filter, range);
        var filteredCount = await query.LongCountAsync(cancellationToken);
        var chartRows = await query.Select(x => new { At = x.CompletedAt ?? x.CreatedAt, x.Type, x.Status })
            .ToListAsync(cancellationToken);
        if (TryDecodeCursor(filter.Cursor, out var cursorAt, out var cursorId))
            query = query.Where(x => (x.CompletedAt ?? x.CreatedAt) < cursorAt ||
                ((x.CompletedAt ?? x.CreatedAt) == cursorAt && x.WalletTransactionsId.CompareTo(cursorId) < 0));
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var transactions = await query.Include(x => x.User).Include(x => x.Contract)
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt).ThenByDescending(x => x.WalletTransactionsId)
            .Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = transactions.Count > pageSize;
        if (hasMore) transactions.RemoveAt(transactions.Count - 1);
        var ids = transactions.Select(x => x.WalletTransactionsId).ToList();
        var sources = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .Where(x => x.WalletTransactionId != null && ids.Contains(x.WalletTransactionId.Value))
            .ToDictionaryAsync(x => x.WalletTransactionId!.Value, x => x.Source.ToString(), cancellationToken);
        var items = transactions.Select(x => new AdminTransactionItem(
            x.WalletTransactionsId, x.CompletedAt ?? x.CreatedAt, x.UserId, x.User.FullName,
            x.ContractsId, x.Contract?.Title, x.Type, Humanize(((WalletTransactionType)x.Type).ToString()),
            x.Status, Humanize(((WalletTransactionStatus)x.Status).ToString()), Direction(x.Type),
            x.TokenAmount, x.VndAmount, x.GatewayProvider,
            x.GatewayTransactionCode ?? x.IdempotencyKey, x.Note, x.Metadata,
            sources.GetValueOrDefault(x.WalletTransactionsId))).ToList();
        var nextCursor = hasMore && transactions.Count > 0
            ? EncodeCursor(transactions[^1].CompletedAt ?? transactions[^1].CreatedAt, transactions[^1].WalletTransactionsId)
            : null;
        var typeBreakdown = chartRows.GroupBy(x => x.Type).Select(x => new AnalyticsBreakdown(
            x.Key.ToString(CultureInfo.InvariantCulture), Humanize(((WalletTransactionType)x.Key).ToString()), x.LongCount(), x.LongCount())).ToList();
        var statusBreakdown = chartRows.GroupBy(x => x.Status).Select(x => new AnalyticsBreakdown(
            x.Key.ToString(CultureInfo.InvariantCulture), Humanize(((WalletTransactionStatus)x.Key).ToString()), x.LongCount(), x.LongCount())).ToList();
        var countSeries = chartRows.GroupBy(x => new { Bucket = AdminAnalyticsRangeResolver.Bucket(x.At, range), x.Type })
            .Select(x => new AnalyticsSeriesPoint(x.Key.Bucket, ((WalletTransactionType)x.Key.Type).ToString(), x.Count()))
            .OrderBy(x => x.Bucket).ToList();
        return new AdminTransactionPage(await BuildMeta(range, cancellationToken), items, nextCursor, pageSize,
            filteredCount, typeBreakdown, statusBreakdown, countSeries);
    }

    public async Task<string> ExportTransactionsAsync(AdminTransactionFilter filter, CancellationToken cancellationToken)
    {
        var csv = new StringBuilder("OccurredAt,UserId,User,ContractId,Contract,Type,Status,Direction,GigCoinAmount,VndAmount,Gateway,Reference,RevenueSource,Note\r\n");
        var range = AdminAnalyticsRangeResolver.Resolve(filter.Range, _clock.UtcNow);
        DateTime? cursorAt = null;
        Guid? cursorId = null;
        const int exportPageSize = 500;
        do
        {
            var query = FilterTransactions(filter with { Cursor = null }, range);
            if (cursorAt.HasValue && cursorId.HasValue)
                query = query.Where(x => (x.CompletedAt ?? x.CreatedAt) < cursorAt.Value ||
                    ((x.CompletedAt ?? x.CreatedAt) == cursorAt.Value &&
                     x.WalletTransactionsId.CompareTo(cursorId.Value) < 0));
            var transactions = await query.Include(x => x.User).Include(x => x.Contract)
                .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.WalletTransactionsId)
                .Take(exportPageSize)
                .ToListAsync(cancellationToken);
            var ids = transactions.Select(x => x.WalletTransactionsId).ToList();
            var sources = ids.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Set<PlatformRevenueEvent>().AsNoTracking()
                    .Where(x => x.WalletTransactionId != null && ids.Contains(x.WalletTransactionId.Value))
                    .ToDictionaryAsync(x => x.WalletTransactionId!.Value, x => x.Source.ToString(), cancellationToken);
            foreach (var item in transactions)
            {
                csv.AppendJoin(',', new[]
                {
                    Csv((item.CompletedAt ?? item.CreatedAt).ToString("O")), Csv(item.UserId.ToString()), Csv(item.User.FullName),
                    Csv(item.ContractsId?.ToString()), Csv(item.Contract?.Title), Csv(Humanize(((WalletTransactionType)item.Type).ToString())),
                    Csv(Humanize(((WalletTransactionStatus)item.Status).ToString())), Csv(Direction(item.Type)),
                    Csv(item.TokenAmount.ToString(CultureInfo.InvariantCulture)), Csv(item.VndAmount.ToString(CultureInfo.InvariantCulture)),
                    Csv(item.GatewayProvider), Csv(item.GatewayTransactionCode ?? item.IdempotencyKey),
                    Csv(sources.GetValueOrDefault(item.WalletTransactionsId)), Csv(item.Note)
                }).Append("\r\n");
            }
            if (transactions.Count < exportPageSize) break;
            cursorAt = transactions[^1].CompletedAt ?? transactions[^1].CreatedAt;
            cursorId = transactions[^1].WalletTransactionsId;
        } while (true);
        return csv.ToString();
    }

    public async Task<MarketplaceAnalyticsResponse> GetMarketplaceAsync(
        AdminAnalyticsRangeRequest request,
        CancellationToken cancellationToken)
    {
        var range = AdminAnalyticsRangeResolver.Resolve(request, _clock.UtcNow);
        var rawSearchRows = await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking()
            .Where(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc &&
                        x.Type == MarketplaceAnalyticsEventType.Search && x.NormalizedQuery != null)
            .GroupBy(x => x.NormalizedQuery!)
            .Select(group => new
            {
                Query = group.Key,
                Searches = group.LongCount(),
                Actors = group.Select(x => x.ActorKey).Distinct().LongCount(),
                ZeroResults = group.LongCount(x => x.ResultCount == 0),
                ResultTotal = group.Sum(x => (long)(x.ResultCount ?? 0))
            })
            .ToListAsync(cancellationToken);
        var rawSearches = rawSearchRows.Select(row => new SearchAccumulator(
            row.Query, row.Searches, row.Actors, row.ZeroResults, row.ResultTotal)).ToList();
        var aggregateFrom = AdminAnalyticsRangeResolver.ToLocalDate(range.CurrentFromUtc);
        var aggregateTo = AdminAnalyticsRangeResolver.ToLocalDate(range.CurrentToUtc.AddTicks(-1));
        var privacyAggregateRows = await _context.Set<MarketplaceAnalyticsDailyAggregate>().AsNoTracking()
            .Where(x => x.DimensionType == "query" && x.Date >= aggregateFrom && x.Date <= aggregateTo &&
                        x.Date < AdminAnalyticsRangeResolver.ToLocalDate(_clock.UtcNow.AddMonths(-13)))
            .GroupBy(x => new { x.DimensionKey, x.Label })
            .Select(group => new
            {
                group.Key.Label,
                Searches = group.Sum(x => x.SearchCount),
                Actors = group.Max(x => x.DistinctActorCount),
                ZeroResults = group.Sum(x => x.ZeroResultCount),
                ResultTotal = group.Sum(x => x.ResultCountTotal)
            })
            .ToListAsync(cancellationToken);
        var privacySearches = privacyAggregateRows.Select(row => new SearchAccumulator(
            row.Label, row.Searches, row.Actors, row.ZeroResults, row.ResultTotal));
        var searches = rawSearches.Concat(privacySearches)
            .GroupBy(x => x.Query, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var count = group.Sum(x => x.Searches);
                // Rolled-up rows do not retain actor identities. Max is conservative and cannot
                // turn one actor seen on several days into several distinct actors.
                var actors = ConservativeDistinctActorCount(group.Select(x => x.Actors));
                var zeros = group.Sum(x => x.ZeroResults);
                var average = count == 0 ? 0m : group.Sum(x => x.ResultTotal) / (decimal)count;
                return new MarketplaceSearchMetric(group.First().Query, count, actors, zeros, average,
                    count * (1m + (count == 0 ? 0m : zeros / (decimal)count)) / (1m + average));
            })
            .Where(x => x.Searches >= 5 && x.DistinctActors >= 3)
            .OrderByDescending(x => x.Searches).ThenByDescending(x => x.OpportunityScore).Take(25).ToList();

        var saved = await _context.Set<SavedJob>().AsNoTracking()
            .Where(x => x.CreatedAt >= range.CurrentFromUtc && x.CreatedAt < range.CurrentToUtc)
            .Select(x => new { x.JobPostsId, x.CreatedAt }).ToListAsync(cancellationToken);
        var proposals = await _context.Set<Proposal>().AsNoTracking()
            .Where(x => x.SubmittedAt != null && x.SubmittedAt >= range.CurrentFromUtc && x.SubmittedAt < range.CurrentToUtc)
            .Select(x => new { x.JobPostsId, At = x.SubmittedAt!.Value }).ToListAsync(cancellationToken);
        var contracts = await _context.Set<Contract>().AsNoTracking()
            .Where(x => x.CreatedAt >= range.CurrentFromUtc && x.CreatedAt < range.CurrentToUtc)
            .Select(x => new { x.JobPostsId, x.CreatedAt }).ToListAsync(cancellationToken);
        var views = await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking()
            .Where(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc &&
                        x.Type == MarketplaceAnalyticsEventType.JobOpen && x.JobPostId != null)
            .Select(x => new { JobPostId = x.JobPostId!.Value, x.ActorKey, x.OccurredAt })
            .ToListAsync(cancellationToken);
        var jobIds = views.Select(x => x.JobPostId).Concat(saved.Select(x => x.JobPostsId))
            .Concat(proposals.Select(x => x.JobPostsId)).Concat(contracts.Select(x => x.JobPostsId)).Distinct().ToList();
        var jobs = await _context.Set<JobPost>().AsNoTracking().Where(x => jobIds.Contains(x.JobPostsId))
            .Select(x => new { x.JobPostsId, x.Title }).ToListAsync(cancellationToken);
        var viewsByJob = views.GroupBy(x => x.JobPostId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.ActorKey).Distinct().LongCount());
        var savesByJob = saved.GroupBy(x => x.JobPostsId).ToDictionary(group => group.Key, group => group.LongCount());
        var proposalsByJob = proposals.GroupBy(x => x.JobPostsId).ToDictionary(group => group.Key, group => group.LongCount());
        var contractsByJob = contracts.GroupBy(x => x.JobPostsId).ToDictionary(group => group.Key, group => group.LongCount());
        var viewSparkline = views.GroupBy(x => new
            {
                x.JobPostId,
                Date = AdminAnalyticsRangeResolver.ToLocalDate(x.OccurredAt)
            }).ToDictionary(group => (group.Key.JobPostId, group.Key.Date), group => group.LongCount());
        var raw = jobs.Select(job => new
        {
            Job = job,
            Views = viewsByJob.GetValueOrDefault(job.JobPostsId),
            Saves = savesByJob.GetValueOrDefault(job.JobPostsId),
            Proposals = proposalsByJob.GetValueOrDefault(job.JobPostsId),
            Contracts = contractsByJob.GetValueOrDefault(job.JobPostsId)
        }).ToList();
        var trending = raw.Select(item =>
        {
            var score = 100m * (0.20m * Percentile(raw.Select(x => x.Views), item.Views) +
                                0.20m * Percentile(raw.Select(x => x.Saves), item.Saves) +
                                0.35m * Percentile(raw.Select(x => x.Proposals), item.Proposals) +
                                0.25m * Percentile(raw.Select(x => x.Contracts), item.Contracts));
            var conversion = item.Views == 0 ? 0m : item.Contracts * 100m / item.Views;
            var sparkline = Enumerable.Range(0, 7).Select(offset =>
            {
                var day = AdminAnalyticsRangeResolver.ToLocalDate(_clock.UtcNow).AddDays(offset - 6);
                return viewSparkline.GetValueOrDefault((item.Job.JobPostsId, day));
            }).ToList();
            return new TrendingJobMetric(item.Job.JobPostsId, item.Job.Title, decimal.Round(score, 2),
                item.Views, item.Saves, item.Proposals, item.Contracts, decimal.Round(conversion, 2), sparkline);
        }).OrderByDescending(x => x.Score).Take(30).ToList();

        var skillDemand = await _context.Set<JobPostSkill>().AsNoTracking()
            .Where(x => x.JobPosts.Status == 1)
            .Select(x => new { x.SkillsId, x.Skills.Name, x.JobPostsId }).ToListAsync(cancellationToken);
        var skillSupply = await _context.Set<FreelancerSkill>().AsNoTracking()
            .Where(x => x.Freelancer.User.IsActive && x.Freelancer.Availability != 2)
            .Select(x => new { x.SkillsId, x.FreelancerId }).ToListAsync(cancellationToken);
        var supplyBySkill = skillSupply.GroupBy(x => x.SkillsId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.FreelancerId).Distinct().LongCount());
        var skillOpportunities = skillDemand.GroupBy(x => new { x.SkillsId, x.Name }).Select(group =>
        {
            var demand = group.Select(x => x.JobPostsId).Distinct().LongCount();
            var supply = supplyBySkill.GetValueOrDefault(group.Key.SkillsId);
            var relatedIds = group.Select(x => x.JobPostsId).ToHashSet();
            var proposalCount = relatedIds.Sum(id => proposalsByJob.GetValueOrDefault(id));
            var contractCount = relatedIds.Sum(id => contractsByJob.GetValueOrDefault(id));
            return new SupplyGapMetric("skill", group.Key.SkillsId.ToString(), group.Key.Name,
                decimal.Round(demand / (decimal)Math.Max(1, supply), 2), demand, supply, demand, proposalCount, contractCount);
        }).OrderByDescending(x => x.Score).Take(20);
        var queryOpportunities = searches.OrderByDescending(x => x.OpportunityScore).Take(20).Select(x =>
            new SupplyGapMetric("query", x.Query, x.Query, decimal.Round(x.OpportunityScore, 2), x.Searches,
                0, decimal.ToInt64(decimal.Round(x.AverageResultCount)), 0, 0));
        var funnel = new MarketplaceFunnel(views.LongCount(), saved.LongCount(), proposals.LongCount(), contracts.LongCount());
        return new MarketplaceAnalyticsResponse(await BuildMarketplaceMeta(range, cancellationToken), searches, trending, funnel,
            queryOpportunities.Concat(skillOpportunities).ToList());
    }

    private IQueryable<WalletTransaction> FilterTransactions(AdminTransactionFilter filter, ResolvedAdminAnalyticsRange range)
    {
        var query = _context.Set<WalletTransaction>().AsNoTracking()
            .Where(x => (x.CompletedAt ?? x.CreatedAt) >= range.CurrentFromUtc && (x.CompletedAt ?? x.CreatedAt) < range.CurrentToUtc);
        if (filter.UserId != null) query = query.Where(x => x.UserId == filter.UserId);
        if (filter.ContractId != null) query = query.Where(x => x.ContractsId == filter.ContractId);
        if (filter.Type != null) query = query.Where(x => x.Type == filter.Type);
        if (filter.Status != null) query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Gateway)) query = query.Where(x => x.GatewayProvider == filter.Gateway);
        if (filter.RevenueSource != null)
            query = query.Where(x => _context.Set<PlatformRevenueEvent>().Any(r =>
                r.WalletTransactionId == x.WalletTransactionsId && r.Source == filter.RevenueSource));
        return query;
    }

    private Task<List<PlatformRevenueEvent>> RevenueInRange(DateTime from, DateTime to, CancellationToken ct) =>
        _context.Set<PlatformRevenueEvent>().AsNoTracking().Where(x => x.OccurredAt >= from && x.OccurredAt < to).ToListAsync(ct);

    private async Task<decimal> SumTopUps(DateTime from, DateTime to, CancellationToken ct) =>
        await _context.Set<WalletTransaction>().AsNoTracking()
            .Where(x => x.Type == (int)WalletTransactionType.TopUp && x.Status == (int)WalletTransactionStatus.Succeeded &&
                        (x.CompletedAt ?? x.CreatedAt) >= from && (x.CompletedAt ?? x.CreatedAt) < to)
            .SumAsync(x => (decimal?)x.VndAmount, ct) ?? 0m;

    private async Task<decimal> SumWithdrawals(DateTime from, DateTime to, CancellationToken ct) =>
        await _context.Set<WalletWithdrawal>().AsNoTracking()
            .Where(x => x.Status == (int)WithdrawalStatus.Success && (x.CompletedAt ?? x.CreatedAt) >= from && (x.CompletedAt ?? x.CreatedAt) < to)
            .SumAsync(x => (decimal?)x.NetVndAmount, ct) ?? 0m;

    private async Task<AnalyticsResponseMeta> BuildMeta(ResolvedAdminAnalyticsRange range, CancellationToken ct)
    {
        var start = await _context.Set<PlatformRevenueEvent>().AsNoTracking().MinAsync(x => (DateTime?)x.OccurredAt, ct);
        var backfill = await _context.Set<PlatformRevenueEvent>().AsNoTracking().Where(x => x.IsBackfilled)
            .MaxAsync(x => (DateTime?)x.RecordedAt, ct);
        var classified = await _context.Set<PlatformRevenueEvent>().AsNoTracking()
            .LongCountAsync(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc, ct);
        var unclassified = await _context.Set<WalletTransaction>().AsNoTracking().LongCountAsync(x =>
            x.Status == (int)WalletTransactionStatus.Succeeded && x.Type == (int)WalletTransactionType.Adjustment &&
            (x.CompletedAt ?? x.CreatedAt) >= range.CurrentFromUtc && (x.CompletedAt ?? x.CreatedAt) < range.CurrentToUtc &&
            (x.IdempotencyKey != null && x.IdempotencyKey.StartsWith("SERVICE-FEE-") ||
             x.Metadata != null && x.Metadata.Contains("ServiceFee")) &&
            !_context.Set<PlatformRevenueEvent>().Any(r => r.WalletTransactionId == x.WalletTransactionsId), ct);
        return new AnalyticsResponseMeta(range, _clock.UtcNow,
            new AnalyticsAvailability(start, backfill, start == null || start > range.CurrentFromUtc,
                start == null ? "Analytics collection has not started." : start > range.CurrentFromUtc ? "The selected range predates analytics collection." : null),
            classified, unclassified);
    }

    private async Task<AnalyticsResponseMeta> BuildMarketplaceMeta(ResolvedAdminAnalyticsRange range, CancellationToken ct)
    {
        var rawStart = await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking().MinAsync(x => (DateTime?)x.OccurredAt, ct);
        var aggregateStart = await _context.Set<MarketplaceAnalyticsDailyAggregate>().AsNoTracking().MinAsync(x => (DateOnly?)x.Date, ct);
        DateTime? start = rawStart;
        if (aggregateStart is not null)
        {
            var aggregateUtc = AdminAnalyticsRangeResolver.Resolve(
                new AdminAnalyticsRangeRequest("custom", null, aggregateStart, aggregateStart), _clock.UtcNow).CurrentFromUtc;
            if (start is null || aggregateUtc < start) start = aggregateUtc;
        }
        var count = await _context.Set<MarketplaceAnalyticsEvent>().AsNoTracking()
            .LongCountAsync(x => x.OccurredAt >= range.CurrentFromUtc && x.OccurredAt < range.CurrentToUtc, ct);
        return new AnalyticsResponseMeta(range, _clock.UtcNow,
            new AnalyticsAvailability(start, null, start == null || start > range.CurrentFromUtc,
                start == null ? "Marketplace collection begins at deployment." :
                start > range.CurrentFromUtc ? "The selected range predates marketplace collection." : null), count, 0);
    }

    private static AnalyticsKpi Kpi(string key, decimal current, decimal previous, string unit, bool calculateGrowth = true) =>
        new(key, current, previous, calculateGrowth ? Growth(current, previous) : null, unit);

    private static decimal? Growth(decimal current, decimal previous) =>
        previous == 0 ? current == 0 ? 0m : null : decimal.Round((current - previous) / Math.Abs(previous) * 100m, 2);

    private static decimal Percentile(IEnumerable<long> values, long value)
    {
        var ordered = values.OrderBy(x => x).ToList();
        if (ordered.Count == 0 || value == 0 && ordered.All(x => x == 0)) return 0m;
        if (ordered.Count == 1) return value > 0 ? 1m : 0m;
        var below = ordered.Count(x => x < value);
        var equal = ordered.Count(x => x == value);
        return (below + (equal - 1) / 2m) / (ordered.Count - 1);
    }

    internal static long ConservativeDistinctActorCount(IEnumerable<long> rolledUpCounts) =>
        rolledUpCounts.DefaultIfEmpty().Max();

    private static string Direction(int type) => (WalletTransactionType)type switch
    {
        WalletTransactionType.TopUp or WalletTransactionType.AdminCredit or WalletTransactionType.EscrowRefund or WalletTransactionType.WithdrawalRefund => "Credit",
        WalletTransactionType.EscrowHold => "Hold",
        WalletTransactionType.EscrowRelease => "Transfer",
        _ => "Debit"
    };

    private static string Humanize(string value) =>
        string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));

    private static string EncodeCursor(DateTime at, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(at, id))));

    private static bool TryDecodeCursor(string? cursor, out DateTime at, out Guid id)
    {
        at = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var value = JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            if (value is null) return false;
            at = value.At;
            id = value.Id;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { return false; }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')) value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record Cursor(DateTime At, Guid Id);
    private sealed record SearchAccumulator(string Query, long Searches, long Actors, long ZeroResults, long ResultTotal);
}
