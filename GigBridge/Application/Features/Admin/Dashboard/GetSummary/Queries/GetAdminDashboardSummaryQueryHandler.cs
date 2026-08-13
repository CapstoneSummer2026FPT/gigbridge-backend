using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Analytics.Common.Services;
using Application.Features.Admin.Dashboard.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Disputes;
using Domain.Enums.Reports;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Dashboard.GetSummary.Queries;

public sealed class GetAdminDashboardSummaryQueryHandler(
    IApplicationDbContext context,
    IAdminAnalyticsService analytics,
    IDateTimeService clock)
    : IRequestHandler<GetAdminDashboardSummaryQuery, AdminDashboardSummary>
{
    internal static readonly int[] AllowedDays = [7, 30, 90];
    internal static readonly int[] ContractReportQueueStatuses =
    [
        (int)ContractReportAdminStatus.Open,
        (int)ContractReportAdminStatus.UnderReview,
        (int)ContractReportAdminStatus.AwaitingInformation,
        (int)ContractReportAdminStatus.Escalated,
    ];
    internal static readonly int[] WithdrawalQueueStatuses =
    [
        (int)WithdrawalStatus.Pending,
        (int)WithdrawalStatus.SyncRequired,
        (int)WithdrawalStatus.Failed,
    ];

    public async Task<AdminDashboardSummary> Handle(
        GetAdminDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!AllowedDays.Contains(request.Days))
            throw new BadRequestException("Dashboard days must be 7, 30, or 90.");

        var today = AdminAnalyticsRangeResolver.ToLocalDate(clock.UtcNow);
        var from = today.AddDays(-(request.Days - 1));
        var resolvedRange = AdminAnalyticsRangeResolver.Resolve(
            new AdminAnalyticsRangeRequest("custom", null, from, today), clock.UtcNow);

        var currentFrom = resolvedRange.CurrentFromUtc;
        var currentTo = resolvedRange.CurrentToUtc;
        var comparisonFrom = resolvedRange.ComparisonFromUtc;
        var comparisonTo = resolvedRange.ComparisonToUtc;

        var marketplaceUsersQuery = context.Set<User>().AsNoTracking()
            .Where(user => user.Role != (int)UserRole.Admin);
        var marketplaceUserCount = await marketplaceUsersQuery.LongCountAsync(cancellationToken);
        var currentUserCount = await marketplaceUsersQuery
            .LongCountAsync(user => user.CreatedAt >= currentFrom && user.CreatedAt < currentTo, cancellationToken);
        var previousUserCount = await marketplaceUsersQuery
            .LongCountAsync(user => user.CreatedAt >= comparisonFrom && user.CreatedAt < comparisonTo, cancellationToken);

        var jobs = context.Set<JobPost>().AsNoTracking();
        var openJobCount = await jobs.LongCountAsync(job => job.Status == 1, cancellationToken);
        var currentJobCount = await jobs
            .LongCountAsync(job => job.CreatedAt >= currentFrom && job.CreatedAt < currentTo, cancellationToken);
        var previousJobCount = await jobs
            .LongCountAsync(job => job.CreatedAt >= comparisonFrom && job.CreatedAt < comparisonTo, cancellationToken);

        var contracts = context.Set<Contract>().AsNoTracking();
        var activeContractCount = await contracts
            .LongCountAsync(contract => contract.Status == (int)ContractStatus.Active, cancellationToken);
        var currentContractCount = await contracts
            .LongCountAsync(contract => contract.CreatedAt >= currentFrom && contract.CreatedAt < currentTo, cancellationToken);
        var previousContractCount = await contracts
            .LongCountAsync(contract => contract.CreatedAt >= comparisonFrom && contract.CreatedAt < comparisonTo, cancellationToken);

        var userDates = await marketplaceUsersQuery
            .Where(user => user.CreatedAt >= currentFrom && user.CreatedAt < currentTo)
            .Select(user => user.CreatedAt)
            .ToListAsync(cancellationToken);
        var jobDates = await jobs
            .Where(job => job.CreatedAt >= currentFrom && job.CreatedAt < currentTo)
            .Select(job => job.CreatedAt)
            .ToListAsync(cancellationToken);
        var proposalDates = await context.Set<Proposal>().AsNoTracking()
            .Where(proposal => proposal.SubmittedAt != null && proposal.SubmittedAt >= currentFrom && proposal.SubmittedAt < currentTo)
            .Select(proposal => proposal.SubmittedAt!.Value)
            .ToListAsync(cancellationToken);
        var contractDates = await contracts
            .Where(contract => contract.CreatedAt >= currentFrom && contract.CreatedAt < currentTo)
            .Select(contract => contract.CreatedAt)
            .ToListAsync(cancellationToken);

        var activity = BuildActivitySeries(
            from,
            request.Days,
            resolvedRange,
            userDates,
            jobDates,
            proposalDates,
            contractDates);

        var reportQueue = await context.Set<Report>().AsNoTracking().LongCountAsync(
            report => report.Status == (int)ReportStatus.Pending || report.Status == (int)ReportStatus.Reviewing,
            cancellationToken);
        var contractReportQueue = await context.Set<ReportContract>().AsNoTracking().LongCountAsync(
            report => ContractReportQueueStatuses.Contains(report.AdminReviewStatus), cancellationToken);
        var disputeQueue = await context.Set<Dispute>().AsNoTracking().LongCountAsync(
            dispute => dispute.Status < (int)DisputeStatus.Resolved, cancellationToken);
        var withdrawalQueue = await context.Set<WalletWithdrawal>().AsNoTracking().LongCountAsync(
            withdrawal => WithdrawalQueueStatuses.Contains(withdrawal.Status), cancellationToken);

        var finance = await analytics.GetFinanceAsync(
            new AdminAnalyticsRangeRequest("custom", null, from, today), cancellationToken);
        var gmv = finance.Kpis.FirstOrDefault(metric => metric.Key == "marketplaceGmv");

        return new AdminDashboardSummary(
            clock.UtcNow,
            new AdminDashboardRange(
                request.Days,
                currentFrom,
                currentTo,
                comparisonFrom,
                comparisonTo,
                resolvedRange.TimeZone),
            CountMetric(marketplaceUserCount, currentUserCount, previousUserCount),
            CountMetric(openJobCount, currentJobCount, previousJobCount),
            CountMetric(activeContractCount, currentContractCount, previousContractCount),
            new AdminDashboardMoneyMetric(
                gmv?.Value ?? 0,
                gmv?.ComparisonValue ?? 0,
                gmv?.ChangePercent,
                gmv?.Unit ?? "VND"),
            activity,
            new AdminDashboardWorkQueue(reportQueue, contractReportQueue, disputeQueue, withdrawalQueue));
    }

    internal static IReadOnlyList<AdminDashboardActivityPoint> BuildActivitySeries(
        DateOnly from,
        int days,
        ResolvedAdminAnalyticsRange range,
        IEnumerable<DateTime> users,
        IEnumerable<DateTime> jobPosts,
        IEnumerable<DateTime> proposals,
        IEnumerable<DateTime> contracts)
    {
        var points = Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .ToDictionary(
                bucket => bucket,
                bucket => new MutableActivityPoint(bucket));

        Add(users, point => point.Users++);
        Add(jobPosts, point => point.JobPosts++);
        Add(proposals, point => point.Proposals++);
        Add(contracts, point => point.Contracts++);

        return points.Values
            .OrderBy(point => point.Bucket)
            .Select(point => new AdminDashboardActivityPoint(
                point.Bucket,
                point.Users,
                point.JobPosts,
                point.Proposals,
                point.Contracts))
            .ToList();

        void Add(IEnumerable<DateTime> dates, Action<MutableActivityPoint> increment)
        {
            foreach (var date in dates)
            {
                var bucket = AdminAnalyticsRangeResolver.Bucket(date, range);
                if (points.TryGetValue(bucket, out var point)) increment(point);
            }
        }
    }

    private static AdminDashboardCountMetric CountMetric(long value, long current, long previous) =>
        new(value, current, previous, Growth(current, previous));

    internal static decimal? Growth(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100m / previous, 2);

    private sealed class MutableActivityPoint(DateOnly bucket)
    {
        public DateOnly Bucket { get; } = bucket;
        public long Users { get; set; }
        public long JobPosts { get; set; }
        public long Proposals { get; set; }
        public long Contracts { get; set; }
    }
}
