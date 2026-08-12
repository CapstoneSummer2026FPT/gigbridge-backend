using Application.Common.Interfaces.Time;
using Application.Features.Admin.Analytics.Common.DTOs;
using Application.Features.Admin.Analytics.Common.Services;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Premium;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_backend.Application.Features.Admin.Analytics;

public sealed class AdminPremiumPromotionTrackingTests
{
    [Fact]
    public async Task Premium_analytics_returns_client_and_freelancer_promotion_attributes()
    {
        var now = new DateTime(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);
        await using var context = new GigbridgeDbContext(
            new DbContextOptionsBuilder<GigbridgeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var client = NewUser(UserRole.Client, "Client Owner", "client@example.com");
        var freelancer = NewUser(UserRole.Freelancer, "Freelancer Owner", "freelancer@example.com");
        var clientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = client.UserId,
            User = client,
            CreatedAt = now.AddMonths(-1)
        };
        var freelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = freelancer.UserId,
            User = freelancer,
            CreatedAt = now.AddMonths(-1)
        };
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,
            ClientProfiles = clientProfile,
            Title = "Premium client job",
            Description = "Description",
            CreatedAt = now.AddDays(-5)
        };
        var unlinkedFeaturedJob = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = clientProfile.ClientProfilesId,
            ClientProfiles = clientProfile,
            Title = "Featured job without audit row",
            Description = "Description",
            IsFeatured = true,
            FeaturedFrom = now.AddHours(-2),
            FeaturedUntil = now.AddDays(2),
            CreatedAt = now.AddDays(-10)
        };
        context.AddRange(client, freelancer, clientProfile, freelancerProfile, jobPost, unlinkedFeaturedJob);
        context.Set<JobPostPromotion>().Add(new JobPostPromotion
        {
            JobPostPromotionsId = Guid.NewGuid(),
            JobPostId = jobPost.JobPostsId,
            JobPost = jobPost,
            ClientUserId = client.UserId,
            ClientUser = client,
            WalletTransactionId = Guid.NewGuid(),
            IdempotencyKey = "job-promotion-test",
            TokenCost = 50,
            PromotionTitle = "Featured client role",
            PromotionDescription = "Client promotion details",
            ImageUrl = "https://example.com/job.png",
            ImpressionCount = 100,
            ClickCount = 10,
            FeaturedFrom = now.AddDays(-1),
            FeaturedUntil = now.AddDays(1),
            CreatedAt = now.AddDays(-1)
        });
        context.Set<FreelancerProfilePromotion>().Add(new FreelancerProfilePromotion
        {
            FreelancerProfilePromotionsId = Guid.NewGuid(),
            FreelancerProfileId = freelancerProfile.FreelancerProfilesId,
            FreelancerProfile = freelancerProfile,
            PackageId = "featured-7",
            PackageName = "Featured 7 days",
            PurchaseIdempotencyKey = "profile-promotion-test",
            DurationDays = 7,
            TokenCost = 30,
            BoostWeight = 1.5m,
            QueuePosition = 2,
            TargetClickCount = 20,
            PhotoUrl = "https://example.com/profile.png",
            DisplayName = "Featured Freelancer",
            Quote = "Available now",
            ShowQuote = true,
            JobTitle = "Backend Engineer",
            ShowJobTitle = true,
            StartTime = now.AddHours(-1),
            EndTime = now.AddDays(7),
            Status = PromotionStatus.Active,
            ImpressionCount = 50,
            ClickCount = 5,
            CreatedAt = now.AddHours(-1)
        });
        await context.SaveChangesAsync();
        var service = new AdminAnalyticsService(context, new FixedClock(now));

        var result = await service.GetPremiumAsync(
            new AdminAnalyticsRangeRequest("custom", null, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10)),
            CancellationToken.None);

        Assert.Equal(3, result.PromotionRecordCount);
        Assert.False(result.PromotionsTruncated);
        Assert.Contains(result.PromotionSummaries, item => item.Role == "Client" && item.Active == 2 && item.ClickThroughRate == 10m);
        Assert.Contains(result.PromotionSummaries, item => item.Role == "Freelancer" && item.Active == 1 && item.ClickThroughRate == 10m);
        var jobPromotion = Assert.Single(result.Promotions, item => item.Type == "Job promotion");
        Assert.Equal("Featured client role", jobPromotion.Attributes["Promotion title"]);
        var unlinkedPromotion = Assert.Single(result.Promotions, item => item.Type == "Job promotion (unlinked)");
        Assert.Equal("Job post featured state", unlinkedPromotion.Attributes["Data source"]);
        var profilePromotion = Assert.Single(result.Promotions, item => item.Role == "Freelancer");
        Assert.Equal("Featured 7 days", profilePromotion.Attributes["Package"]);
        Assert.Equal("Backend Engineer", profilePromotion.Attributes["Job title"]);
    }

    private static User NewUser(UserRole role, string name, string email) => new()
    {
        UserId = Guid.NewGuid(),
        FullName = name,
        Email = email,
        Role = (int)role,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class FixedClock(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
