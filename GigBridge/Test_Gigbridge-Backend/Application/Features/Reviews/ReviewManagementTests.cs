using Application.Common.Interfaces.IService;
using Application.Common.Services;
using Application.Features.Reviews.Admin.GetReviews.Queries;
using Application.Features.Reviews.Common.GetMyReviews.Queries;
using Application.Features.Reviews.Common.GetReviewsByUser.Queries;
using Application.Features.Reviews.Common.GetReviewStats.Queries;
using Application.Features.Reviews.Common.Moderation;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Application.Features.Reviews;

public class ReviewManagementTests
{
    [Fact]
    public async Task MyReviews_SeparatesDirectionsAndKeepsPrivateModerationState()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var received = AddReview(context, seed.Contract, seed.Freelancer, seed.Client, 5, hidden: true, anonymous: true);
        AddReview(context, seed.Contract, seed.Client, seed.Freelancer, 4);
        context.Reports.Add(new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = seed.Client.UserId,
            ReportedEntityId = received.ReviewsId,
            ReportedEntityType = ReportedEntityTypes.Review,
            Type = (int)ReportType.InappropriateContent,
            Reason = "Review contains inappropriate content.",
            Status = (int)ReportStatus.Pending,
            CreatedAt = seed.Now
        });
        await context.SaveChangesAsync();

        var handler = new GetMyReviewsQueryHandler(context);
        var result = await handler.Handle(
            new GetMyReviewsQuery(seed.Client.UserId, "received", 1, 10),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(received.ReviewsId, item.ReviewId);
        Assert.Equal(ReviewModerationStatus.Hidden, item.ModerationStatus);
        Assert.Equal("Anonymous User", item.ReviewerName);
        Assert.True(item.HasOpenReport);

        var sent = await handler.Handle(
            new GetMyReviewsQuery(seed.Client.UserId, "sent", 1, 10),
            CancellationToken.None);
        Assert.Single(sent.Items);
        Assert.False(sent.Items[0].HasOpenReport);
    }

    [Fact]
    public async Task PublicReviewsAndStats_ExcludeModeratedButKeepLegacyAnonymous()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        AddReview(context, seed.Contract, seed.Freelancer, seed.Client, 5, hidden: true);
        AddReview(context, seed.Contract, seed.Freelancer, seed.Client, 3, anonymous: true);
        await context.SaveChangesAsync();

        var reviews = await new GetReviewsByUserQueryHandler(context).Handle(
            new GetReviewsByUserQuery(seed.Client.UserId),
            CancellationToken.None);
        var item = Assert.Single(reviews);
        Assert.True(item.IsAnonymous);
        Assert.Equal(3, item.Rating);

        var stats = await new GetReviewStatsQueryHandler(context).Handle(
            new GetReviewStatsQuery(seed.Client.UserId),
            CancellationToken.None);
        Assert.Equal(1, stats.TotalReviews);
        Assert.Equal(3, stats.AverageRating);
    }

    [Fact]
    public async Task AdminReviews_RevealsLegacyAnonymousIdentityAndFiltersOpenReports()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var review = AddReview(context, seed.Contract, seed.Freelancer, seed.Client, 4, anonymous: true);
        context.Reports.Add(new Report
        {
            ReportsId = Guid.NewGuid(),
            ReporterId = seed.Client.UserId,
            ReportedEntityId = review.ReviewsId,
            ReportedEntityType = ReportedEntityTypes.Review,
            Type = (int)ReportType.Other,
            Reason = "Please review this feedback.",
            Status = (int)ReportStatus.Reviewing,
            CreatedAt = seed.Now
        });
        await context.SaveChangesAsync();

        var result = await new GetAdminReviewsQueryHandler(context).Handle(
            new GetAdminReviewsQuery(HasOpenReport: true),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Freelancer User", item.ReviewerName);
        Assert.True(item.IsAnonymous);
        Assert.Equal(1, item.OpenReportCount);
        Assert.Equal(1, result.Summary.WithOpenReports);
    }

    [Fact]
    public async Task Moderation_HideAndRestoreCompensatesActualEloAndIsIdempotent()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var review = AddReview(context, seed.Contract, seed.Client, seed.Freelancer, 5);
        await context.SaveChangesAsync();
        var clock = new FixedDateTimeService(seed.Now);
        var elo = new UserEloService(context, clock);
        await elo.ApplyReviewScoreAsync(review.ReviewsId, seed.Freelancer.UserId, 5, CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(170, (await context.UserEloScores.SingleAsync()).CurrentPoints);

        var service = new ReviewModerationService(context, clock, elo);
        var hidden = await service.SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Hidden,
            seed.Admin.UserId,
            "The review violates the platform policy.",
            CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.True(hidden.Changed);
        Assert.Equal(-70, hidden.EloDelta);
        Assert.Equal(100, (await context.UserEloScores.SingleAsync()).CurrentPoints);
        Assert.Single(context.AdminAuditLogs);

        var repeated = await service.SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Hidden,
            seed.Admin.UserId,
            "Repeated request must be ignored.",
            CancellationToken.None);
        Assert.False(repeated.Changed);
        Assert.Single(context.UserEloPointTransactions.Where(item => item.Reason == (int)UserEloPointReason.ReviewModeration));

        var restored = await service.SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Active,
            seed.Admin.UserId,
            "The review was restored after another check.",
            CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(70, restored.EloDelta);
        Assert.Equal(170, (await context.UserEloScores.SingleAsync()).CurrentPoints);
        Assert.Equal(2, context.AdminAuditLogs.Count());
    }

    [Fact]
    public async Task Moderation_NegativeReviewUsesEffectiveFloorDeltaWhenHiddenAndRestored()
    {
        await using var context = CreateContext();
        var seed = await SeedAsync(context);
        var review = AddReview(context, seed.Contract, seed.Client, seed.Freelancer, 1);
        context.UserEloScores.Add(new UserEloScore
        {
            UserEloScoresId = Guid.NewGuid(),
            UserId = seed.Freelancer.UserId,
            CurrentPoints = 0,
            LastActivityAt = seed.Now,
            CreatedAt = seed.Now
        });
        context.UserEloPointTransactions.Add(new UserEloPointTransaction
        {
            UserEloPointTransactionsId = Guid.NewGuid(),
            UserId = seed.Freelancer.UserId,
            PointsDelta = -20,
            PointsBefore = 20,
            PointsAfter = 0,
            Reason = (int)UserEloPointReason.ReviewRating,
            SourceEntityType = "Review",
            SourceEntityId = review.ReviewsId,
            IdempotencyKey = $"review:{review.ReviewsId}:floor-rating",
            CreatedAt = seed.Now
        });
        await context.SaveChangesAsync();
        var clock = new FixedDateTimeService(seed.Now);
        var service = new ReviewModerationService(context, clock, new UserEloService(context, clock));

        var hidden = await service.SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Hidden,
            seed.Admin.UserId,
            "Hide a negative review after moderation.",
            CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(20, hidden.EloDelta);
        Assert.Equal(20, (await context.UserEloScores.SingleAsync()).CurrentPoints);

        var restored = await service.SetStatusAsync(
            review.ReviewsId,
            ReviewModerationStatus.Active,
            seed.Admin.UserId,
            "Restore the negative review after appeal.",
            CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(-20, restored.EloDelta);
        Assert.Equal(0, (await context.UserEloScores.SingleAsync()).CurrentPoints);
    }

    private static GigbridgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static async Task<Seed> SeedAsync(GigbridgeDbContext context)
    {
        var now = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var client = AddUser(UserRole.Client, "Client User", now);
        var freelancer = AddUser(UserRole.Freelancer, "Freelancer User", now);
        var admin = AddUser(UserRole.Admin, "Admin User", now);
        var clientProfile = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = client.UserId, User = client, CreatedAt = now };
        var freelancerProfile = new FreelancerProfile { FreelancerProfilesId = Guid.NewGuid(), UserId = freelancer.UserId, User = freelancer, CreatedAt = now };
        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            ClientProfiles = clientProfile,
            FreelancerProfiles = freelancerProfile,
            Title = "Managed review project",
            TotalBudget = 1000,
            Status = (int)ContractStatus.Completed,
            CompletedAt = now,
            CreatedAt = now
        };
        context.Users.AddRange(client, freelancer, admin);
        context.ClientProfiles.Add(clientProfile);
        context.FreelancerProfiles.Add(freelancerProfile);
        context.Contracts.Add(contract);
        await context.SaveChangesAsync();
        return new Seed(now, client, freelancer, admin, contract);
    }

    private static User AddUser(UserRole role, string name, DateTime now) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = name,
        Email = $"{Guid.NewGuid():N}@example.com",
        Role = (int)role,
        IsActive = true,
        CreatedAt = now
    };

    private static Review AddReview(
        GigbridgeDbContext context,
        Contract contract,
        User reviewer,
        User reviewee,
        int rating,
        bool hidden = false,
        bool anonymous = false)
    {
        var review = new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            Contracts = contract,
            ReviewerId = reviewer.UserId,
            Reviewer = reviewer,
            RevieweeId = reviewee.UserId,
            Reviewee = reviewee,
            Rating = rating,
            CommunicationRating = rating,
            QualityRating = rating,
            TimelinessRating = rating,
            Comment = "Detailed project feedback.",
            IsVisible = !anonymous,
            ModerationStatus = hidden ? (int)ReviewModerationStatus.Hidden : (int)ReviewModerationStatus.Active,
            CreatedAt = contract.CompletedAt!.Value.AddMinutes(rating)
        };
        context.Reviews.Add(review);
        return review;
    }

    private sealed record Seed(DateTime Now, User Client, User Freelancer, User Admin, Contract Contract);

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; }
    }
}
