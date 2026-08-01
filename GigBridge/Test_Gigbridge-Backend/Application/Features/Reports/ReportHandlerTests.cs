using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Reports.ResolveReport.Commands;
using Application.Features.Reviews.Common.Moderation;
using Application.Features.Admin.Reports.ResolveReport.DTOs;
using Application.Features.Admin.Reports.GetReports.Queries;
using Application.Features.Admin.Reports.GetReportSummary.Queries;
using Application.Features.Admin.Reports.UpdateReportStatus.Commands;
using Application.Features.Admin.Reports.UpdateReportStatus.DTOs;
using Application.Features.Reports.Public.CreateReport.Commands;
using Application.Features.Reports.Public.CreateReport.DTOs;
using Application.Features.Reports.Public.GetReportDetail.Queries;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Reports;

public class ReportHandlerTests
{
    [Fact]
    public async Task GetReports_FiltersByExactReportedEntityId()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var matchingUser = AddUser(context, UserRole.Freelancer);
        var otherUser = AddUser(context, UserRole.Freelancer);
        AddReport(context, reporter.UserId, matchingUser.UserId, ReportedEntityTypes.User);
        AddReport(context, reporter.UserId, otherUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new GetReportsQueryHandler(context);
        var result = await handler.Handle(
            new GetReportsQuery(ReportedEntityType: ReportedEntityTypes.User, ReportedEntityId: matchingUser.UserId),
            CancellationToken.None);

        var report = Assert.Single(result.Items);
        Assert.Equal(matchingUser.UserId, report.ReportedEntityId);
    }

    [Fact]
    public async Task GetReportSummary_CountsEveryStatusAndOpenReports()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var target = AddUser(context, UserRole.Freelancer);

        foreach (var status in Enum.GetValues<ReportStatus>())
        {
            var report = AddReport(context, reporter.UserId, target.UserId, ReportedEntityTypes.User);
            report.Status = (int)status;
        }
        await context.SaveChangesAsync();

        var handler = new GetReportSummaryQueryHandler(context);
        var result = await handler.Handle(new GetReportSummaryQuery(), CancellationToken.None);

        Assert.Equal(4, result.Total);
        Assert.Equal(1, result.Pending);
        Assert.Equal(1, result.Reviewing);
        Assert.Equal(1, result.Resolved);
        Assert.Equal(1, result.Dismissed);
        Assert.Equal(2, result.Open);
    }

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
    public async Task CreateReport_AllowsOnlyReviewRecipientToReportReview()
    {
        await using var context = CreateContext();
        var recipient = AddUser(context, UserRole.Client);
        var unrelatedUser = AddUser(context, UserRole.Client);
        var review = AddReview(context, recipient.UserId);
        await context.SaveChangesAsync();
        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());

        await handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(review.ReviewsId, ReportedEntityTypes.Review, ReportType.Other, "This feedback violates policy."),
                recipient.UserId),
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(review.ReviewsId, ReportedEntityTypes.Review, ReportType.Other, "I do not own this feedback."),
                unrelatedUser.UserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateReport_CreatesReportForPublicOpenJobPost()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Freelancer);
        var owner = AddUser(context, UserRole.Client);
        var jobPost = AddJobPost(context, owner.UserId);
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());
        var reportId = await handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(jobPost.JobPostsId, ReportedEntityTypes.JobPost, ReportType.Fraud, "Suspicious job."),
                reporter.UserId),
            CancellationToken.None);

        var report = await context.Reports.SingleAsync(report => report.ReportsId == reportId);
        Assert.Equal(jobPost.JobPostsId, report.ReportedEntityId);
        Assert.Equal(ReportedEntityTypes.JobPost, report.ReportedEntityType);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public async Task CreateReport_RejectsJobPostHiddenFromReporter(int status, int? visibility)
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Freelancer);
        var owner = AddUser(context, UserRole.Client);
        var jobPost = AddJobPost(context, owner.UserId, status, visibility);
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(jobPost.JobPostsId, ReportedEntityTypes.JobPost, ReportType.Fraud, "Hidden job."),
                reporter.UserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateReport_AllowsOwningClientToReportOwnHiddenJobPost()
    {
        await using var context = CreateContext();
        var owner = AddUser(context, UserRole.Client);
        var jobPost = AddJobPost(context, owner.UserId, status: 0, visibility: 1);
        await context.SaveChangesAsync();

        var handler = new CreateReportCommandHandler(context, new FixedDateTimeService());
        var reportId = await handler.Handle(
            new CreateReportCommand(
                new CreateReportRequest(jobPost.JobPostsId, ReportedEntityTypes.JobPost, ReportType.Other, "Own draft issue."),
                owner.UserId),
            CancellationToken.None);

        var report = await context.Reports.SingleAsync(report => report.ReportsId == reportId);
        Assert.Equal(jobPost.JobPostsId, report.ReportedEntityId);
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

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService(), Substitute.For<IReviewModerationService>());
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
    public async Task UpdateReportStatus_DismissedRecordsAdminAndClosureMetadata()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new UpdateReportStatusCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new UpdateReportStatusCommand(
                report.ReportsId,
                admin.UserId,
                new UpdateReportStatusRequest(ReportStatus.Dismissed, " Not actionable. ")),
            CancellationToken.None);

        var dismissedReport = await context.Reports.SingleAsync(item => item.ReportsId == report.ReportsId);
        Assert.Equal((int)ReportStatus.Dismissed, dismissedReport.Status);
        Assert.Equal(admin.UserId, dismissedReport.ResolvedByAdminId);
        Assert.Equal(new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc), dismissedReport.ResolvedAt);
        Assert.Equal(new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc), dismissedReport.UpdatedAt);
        Assert.Equal("Not actionable.", dismissedReport.AdminNote);
    }

    [Fact]
    public async Task UpdateReportStatus_DismissedRejectsNonAdmin()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var nonAdmin = AddUser(context, UserRole.Freelancer);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new UpdateReportStatusCommandHandler(context, new FixedDateTimeService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateReportStatusCommand(
                report.ReportsId,
                nonAdmin.UserId,
                new UpdateReportStatusRequest(ReportStatus.Dismissed, null)),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateReportStatus_DismissedRejectsMissingAdmin()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new UpdateReportStatusCommandHandler(context, new FixedDateTimeService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateReportStatusCommand(
                report.ReportsId,
                Guid.NewGuid(),
                new UpdateReportStatusRequest(ReportStatus.Dismissed, null)),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateReportStatus_ReviewingDoesNotSetClosureMetadata()
    {
        await using var context = CreateContext();
        var reporter = AddUser(context, UserRole.Client);
        var admin = AddUser(context, UserRole.Admin);
        var reportedUser = AddUser(context, UserRole.Freelancer);
        var report = AddReport(context, reporter.UserId, reportedUser.UserId, ReportedEntityTypes.User);
        await context.SaveChangesAsync();

        var handler = new UpdateReportStatusCommandHandler(context, new FixedDateTimeService());
        await handler.Handle(
            new UpdateReportStatusCommand(
                report.ReportsId,
                admin.UserId,
                new UpdateReportStatusRequest(ReportStatus.Reviewing, "Looking into it.")),
            CancellationToken.None);

        var reviewingReport = await context.Reports.SingleAsync(item => item.ReportsId == report.ReportsId);
        Assert.Equal((int)ReportStatus.Reviewing, reviewingReport.Status);
        Assert.Null(reviewingReport.ResolvedByAdminId);
        Assert.Null(reviewingReport.ResolvedAt);
        Assert.NotNull(reviewingReport.UpdatedAt);
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

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService(), Substitute.For<IReviewModerationService>());
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

        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService(), Substitute.For<IReviewModerationService>());
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

        var moderationService = Substitute.For<IReviewModerationService>();
        var handler = new ResolveReportCommandHandler(context, new FixedDateTimeService(), moderationService);
        await handler.Handle(
            new ResolveReportCommand(report.ReportsId, admin.UserId, new ResolveReportRequest("Hide.", true)),
            CancellationToken.None);

        await moderationService.Received(1).SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Hidden,
            admin.UserId,
            "Hide.",
            Arg.Any<CancellationToken>());
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

    private static JobPost AddJobPost(
        GigbridgeDbContext context,
        Guid? ownerUserId = null,
        int status = 1,
        int? visibility = 0)
    {
        var clientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = ownerUserId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,
            Title = "Suspicious job",
            Description = "Suspicious description.",
            Status = status,
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow
        };

        context.ClientProfiles.Add(clientProfile);
        context.JobPosts.Add(jobPost);
        return jobPost;
    }

    private static Review AddReview(GigbridgeDbContext context, Guid? revieweeId = null)
    {
        var review = new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = Guid.NewGuid(),
            ReviewerId = Guid.NewGuid(),
            RevieweeId = revieweeId ?? Guid.NewGuid(),
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
