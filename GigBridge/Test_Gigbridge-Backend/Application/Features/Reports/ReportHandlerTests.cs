using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Reports.ResolveReport.Commands;
using Application.Features.Admin.Reports.ResolveReport.DTOs;
using Application.Features.Reports.Public.CreateReport.Commands;
using Application.Features.Reports.Public.CreateReport.DTOs;
using Application.Features.Reports.Public.GetReportDetail.Queries;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Features.Reports;

public class ReportHandlerTests
{
    [Fact]
    public async Task CreateReport_CreatesReportForExistingUserTarget()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());
        var reportId = await handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(reportedUser.UserId, ReportedEntityTypes.User, ReportType.Spam, " Spam profile "),
                reporter.UserId),
            CancellationToken.None);

        var report = await context.Reports.SingleAsync(report => report.ReportsId == reportId);
        Assert.Equal(reporter.UserId, report.ReporterId);
        Assert.Equal("Spam profile", report.Reason);
        Assert.Equal((int)ReportStatus.Pending, report.Status);
    }

    [Fact]
    public async Task CreateReport_RejectsDuplicateOpenReport()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        context.Reports.Add(new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = reporter.UserId,
            ReportedEntityId = reportedUser.UserId,
            ReportedEntityType = ReportedEntityTypes.User,
            Type = (int)ReportType.Spam,
            Reason = "Already reported.",
            Status = (int)ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(reportedUser.UserId, ReportedEntityTypes.User, ReportType.Fraud, "Again."),
                reporter.UserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateReport_RejectsSelfUserReport()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(reporter.UserId, ReportedEntityTypes.User, ReportType.Other, "My own account."),
                reporter.UserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetReportDetail_RejectsOtherReporter()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var otherUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, otherUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new GetReportDetailQueryHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new GetReportDetailQuery(report.ReportsId, otherUser.UserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task ResolveReport_UpdatesReportFields()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new ResolveReportCommand(report.ReportsId, admin.UserId, new ResolveReportRequest("Handled.", false)),
            CancellationToken.None);

        var resolvedReport = await context.Reports.SingleAsync(item => item.ReportsId == report.ReportsId);
        Assert.Equal((int)ReportStatus.Resolved, resolvedReport.Status);
        Assert.Equal(admin.UserId, resolvedReport.ResolvedByAdminId);
        Assert.Equal("Handled.", resolvedReport.AdminNote);
        Assert.NotNull(resolvedReport.ResolvedAt);
    }

    [Fact]
    public async Task ResolveReport_WithActionDeactivatesReportedUser()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new ResolveReportCommand(report.ReportsId, admin.UserId, new ResolveReportRequest("Deactivate.", true)),
            CancellationToken.None);

        var user = await context.Users.SingleAsync(item => item.UserId == reportedUser.UserId);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task ResolveReport_WithActionCancelsReportedJobPost()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var jobPost = AddJobPost(context);
        var report = AddReport(context, reporter.UserId, jobPost.JobPostsId, ReportedEntityTypes.JobPost);
        await context.SaveChangesAsync();

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new ResolveReportCommand(report.ReportsId, admin.UserId, new ResolveReportRequest("Cancel.", true)),
            CancellationToken.None);

        var updatedJobPost = await context.JobPosts.SingleAsync(item => item.JobPostsId == jobPost.JobPostsId);
        Assert.Equal(3, updatedJobPost.Status);
    }

    [Fact]
    public async Task ResolveReport_WithActionHidesReportedReview()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var review = AddReview(context);
        var report = AddReport(context, reporter.UserId, review.ReviewsId, ReportedEntityTypes.Review);
        await context.SaveChangesAsync();

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new ResolveReportCommand(report.ReportsId, admin.UserId, new ResolveReportRequest("Hide.", true)),
            CancellationToken.None);

        var updatedReview = await context.Reviews.SingleAsync(item => item.ReviewsId == review.ReviewsId);
        Assert.False(updatedReview.IsVisible);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GigbridgeDbContext(options);
    }

    private static User AddUser(GigbridgeDbContext context, UserRole role)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = $"{role} User",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = (int)role,
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(user);
        return user;
    }

    private static Report AddReport(GigbridgeDbContext context, Guid reporterId, Guid targetId, string targetType)
    {
        var report = new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = reporterId,
            ReportedEntityId = targetId,
            ReportedEntityType = targetType,
            Type = (int)ReportType.Other,
            Reason = "Needs review.",
            Status = (int)ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.Reports.Add(report);
        return report;
    }

    private static JobPost AddJobPost(GigbridgeDbContext context)
    {
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            Title = "Suspicious job",
            Description = "Suspicious description.",
            BudgetType = 0,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.JobPosts.Add(jobPost);
        return jobPost;
    }

    private static Review AddReview(GigbridgeDbContext context)
    {
        var review = new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = Guid.NewGuid(),
            ReviewerId = Guid.NewGuid(),
            RevieweeId = Guid.NewGuid(),
            Rating = 1,
            Comment = "Abusive review.",
            IsVisible = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Reviews.Add(review);
        return review;
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public DateTime UtcNow => new(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
    }
}
