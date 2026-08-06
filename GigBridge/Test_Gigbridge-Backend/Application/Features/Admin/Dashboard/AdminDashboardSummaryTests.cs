using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Interfaces;
using Application.Features.Admin.Dashboard.GetSummary.Queries;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_backend.Application.Features.Admin.Dashboard;

public sealed class AdminDashboardSummaryTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(365)]
    public async Task Rejects_unsupported_dashboard_ranges(int days)
    {
        await using var context = CreateContext();
        var handler = new GetAdminDashboardSummaryQueryHandler(
            context,
            Substitute.For<IAdminAnalyticsService>(),
            new FixedClock(Now));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new GetAdminDashboardSummaryQuery(days), CancellationToken.None));
    }

    [Fact]
    public async Task Builds_metrics_zero_filled_activity_and_exact_work_queues()
    {
        await using var context = CreateContext();
        context.Users.AddRange(
            User(UserRole.Client, new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc)),
            User(UserRole.Freelancer, new DateTime(2026, 6, 20, 1, 0, 0, DateTimeKind.Utc)),
            User(UserRole.Admin, new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc)));
        context.JobPosts.AddRange(
            Job(status: 1, createdAt: new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc)),
            Job(status: 2, createdAt: new DateTime(2026, 6, 22, 1, 0, 0, DateTimeKind.Utc)));
        context.Contracts.Add(Contract(
            ContractStatus.Active,
            new DateTime(2026, 8, 3, 1, 0, 0, DateTimeKind.Utc)));
        context.Proposals.Add(Proposal(new DateTime(2026, 8, 4, 1, 0, 0, DateTimeKind.Utc)));
        context.Reports.AddRange(
            Report(ReportStatus.Pending),
            Report(ReportStatus.Resolved));
        context.ReportContracts.AddRange(
            ContractReport(ContractReportAdminStatus.AwaitingInformation),
            ContractReport(ContractReportAdminStatus.Closed));
        context.Disputes.AddRange(
            Dispute(DisputeStatus.DecisionPending),
            Dispute(DisputeStatus.Resolved));
        context.WalletWithdrawals.AddRange(
            Withdrawal(WithdrawalStatus.SyncRequired),
            Withdrawal(WithdrawalStatus.Processing));
        await context.SaveChangesAsync();

        var analytics = Substitute.For<IAdminAnalyticsService>();
        analytics.GetFinanceAsync(Arg.Any<AdminAnalyticsRangeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Finance(120_000m, 80_000m, 50m));
        var handler = new GetAdminDashboardSummaryQueryHandler(context, analytics, new FixedClock(Now));

        var result = await handler.Handle(new GetAdminDashboardSummaryQuery(30), CancellationToken.None);

        Assert.Equal(30, result.Activity.Count);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Range.TimeZone);
        Assert.Equal(new DateTime(2026, 7, 6, 17, 0, 0, DateTimeKind.Utc), result.Range.CurrentFromUtc);
        Assert.Equal(new DateTime(2026, 8, 5, 17, 0, 0, DateTimeKind.Utc), result.Range.CurrentToUtc);
        Assert.Equal(2, result.MarketplaceUsers.Value);
        Assert.Equal(1, result.MarketplaceUsers.PeriodValue);
        Assert.Equal(1, result.OpenJobPosts.Value);
        Assert.Equal(1, result.ActiveContracts.Value);
        Assert.Equal(120_000m, result.MarketplaceGmv.Value);
        Assert.Equal(1, result.Activity.Sum(point => point.Users));
        Assert.Equal(1, result.Activity.Sum(point => point.JobPosts));
        Assert.Equal(1, result.Activity.Sum(point => point.Proposals));
        Assert.Equal(1, result.Activity.Sum(point => point.Contracts));
        Assert.Equal(1, result.WorkQueue.Reports);
        Assert.Equal(1, result.WorkQueue.ContractReports);
        Assert.Equal(1, result.WorkQueue.Disputes);
        Assert.Equal(1, result.WorkQueue.Withdrawals);
    }

    [Fact]
    public async Task Empty_data_returns_zero_metrics_and_one_point_per_day()
    {
        await using var context = CreateContext();
        var analytics = Substitute.For<IAdminAnalyticsService>();
        analytics.GetFinanceAsync(Arg.Any<AdminAnalyticsRangeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Finance(0, 0, null));
        var handler = new GetAdminDashboardSummaryQueryHandler(context, analytics, new FixedClock(Now));

        var result = await handler.Handle(new GetAdminDashboardSummaryQuery(7), CancellationToken.None);

        Assert.Equal(7, result.Activity.Count);
        Assert.All(result.Activity, point => Assert.Equal(0, point.Users + point.JobPosts + point.Proposals + point.Contracts));
        Assert.Equal(0, result.MarketplaceUsers.Value);
        Assert.Null(result.MarketplaceUsers.ChangePercent);
        Assert.Equal(0, result.WorkQueue.Reports + result.WorkQueue.ContractReports + result.WorkQueue.Disputes + result.WorkQueue.Withdrawals);
    }

    [Fact]
    public void Growth_is_null_without_a_comparison_baseline()
    {
        Assert.Null(GetAdminDashboardSummaryQueryHandler.Growth(12, 0));
        Assert.Equal(50m, GetAdminDashboardSummaryQueryHandler.Growth(15, 10));
        Assert.Equal(-50m, GetAdminDashboardSummaryQueryHandler.Growth(5, 10));
    }

    private static GigbridgeDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User User(UserRole role, DateTime createdAt) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = role.ToString(),
        Email = $"{Guid.NewGuid():N}@example.test",
        Role = (int)role,
        IsActive = true,
        CreatedAt = createdAt,
    };

    private static JobPost Job(int status, DateTime createdAt) => new()
    {
        JobPostsId = Guid.NewGuid(),
        ClientProfilesId = Guid.NewGuid(),
        Title = "Job",
        Description = "Job description",
        Status = status,
        CreatedAt = createdAt,
    };

    private static Contract Contract(ContractStatus status, DateTime createdAt) => new()
    {
        ContractsId = Guid.NewGuid(),
        JobPostsId = Guid.NewGuid(),
        ClientProfilesId = Guid.NewGuid(),
        Title = "Contract",
        Status = (int)status,
        CreatedAt = createdAt,
    };

    private static Proposal Proposal(DateTime submittedAt) => new()
    {
        ProposalsId = Guid.NewGuid(),
        JobPostsId = Guid.NewGuid(),
        FreelancerProfilesId = Guid.NewGuid(),
        SubmittedAt = submittedAt,
        Status = (int)ProposalStatus.Pending,
    };

    private static Report Report(ReportStatus status) => new()
    {
        ReportsId = Guid.NewGuid(),
        ReporterId = Guid.NewGuid(),
        ReportedEntityId = Guid.NewGuid(),
        ReportedEntityType = "User",
        Reason = "Reason",
        Status = (int)status,
        CreatedAt = Now,
    };

    private static ReportContract ContractReport(ContractReportAdminStatus status) => new()
    {
        ReportContractId = Guid.NewGuid(),
        ContractId = Guid.NewGuid(),
        ReporterId = Guid.NewGuid(),
        Description = "Description",
        DesiredResolution = "Resolution",
        AdminReviewStatus = (int)status,
        CreatedAt = Now,
    };

    private static Dispute Dispute(DisputeStatus status) => new()
    {
        DisputesId = Guid.NewGuid(),
        ContractsId = Guid.NewGuid(),
        InitiatorId = Guid.NewGuid(),
        Reason = "Reason",
        Status = (int)status,
        CreatedAt = Now,
    };

    private static WalletWithdrawal Withdrawal(WithdrawalStatus status) => new()
    {
        WalletWithdrawalId = Guid.NewGuid(),
        UserWalletsId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        BankCode = "TEST",
        BankName = "Test Bank",
        BankAccountNumberEncrypted = "encrypted",
        BankAccountNumberMasked = "***1234",
        BankAccountName = "Test User",
        ProviderOrderCode = Guid.NewGuid().ToString("N"),
        Status = (int)status,
        CreatedAt = Now,
    };

    private static FinanceAnalyticsResponse Finance(decimal gmv, decimal previous, decimal? change)
    {
        var range = new ResolvedAdminAnalyticsRange(
            "custom", Now.AddDays(-30), Now, Now.AddDays(-60), Now.AddDays(-30),
            "Asia/Ho_Chi_Minh", "day");
        var meta = new AnalyticsResponseMeta(
            range, Now, new AnalyticsAvailability(null, null, false, null), 0, 0);
        return new FinanceAnalyticsResponse(
            meta,
            [new AnalyticsKpi("marketplaceGmv", gmv, previous, change, "VND")],
            [], [], [], [], 0, 0, 0);
    }

    private sealed class FixedClock(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
