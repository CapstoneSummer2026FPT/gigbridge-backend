using Application.Common.Interfaces.Time;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;
using Domain.Entities;
using Domain.Enums.Premium;
using FluentValidation.TestHelper;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public sealed class GetFreelancersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPagedSummary_WithFeaturedPromotionFirst()
    {
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var highestElo = CreateProfile("Highest Elo", now.AddDays(-3), eloPoints: 2_000);
        var promoted = CreateProfile("Promoted", now.AddDays(-2), eloPoints: 900);
        var newest = CreateProfile("Newest", now.AddDays(-1), eloPoints: 800);
        var promotedSkill = CreateSkill(promoted, "React", 3);

        context.AddSet(highestElo, promoted, newest);
        context.AddSet(new FreelancerProfilePromotion
        {
            FreelancerProfilePromotionsId = Guid.NewGuid(),
            FreelancerProfileId = promoted.FreelancerProfilesId,
            BoostWeight = 10m,
            Status = PromotionStatus.Active,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1),
            CreatedAt = now.AddHours(-1)
        });
        context.AddSet<Subscription>();
        context.AddSet<Review>();
        context.AddSet(promotedSkill);
        context.AddSet<FreelancerProfileCategory>();
        context.AddSet<PlatformSetting>();

        var handler = new GetFreelancersQueryHandler(context, new FixedClock(now));

        var result = await handler.Handle(
            new GetFreelancersQuery(Page: 1, PageSize: 1),
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        var item = Assert.Single(result.Items);
        Assert.Equal(promoted.FreelancerProfilesId, item.FreelancerProfilesId);
        Assert.Equal("Promoted", item.UserFullName);
        Assert.Equal("React", Assert.Single(item.Skills).SkillName);
    }

    [Fact]
    public async Task Handle_AppliesSkillAvailabilityRatingAndSearchBeforePagination()
    {
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var matching = CreateProfile("Mai React", now.AddDays(-2), eloPoints: 900);
        matching.Title = "Frontend Engineer";
        matching.Availability = 0;
        var matchingSkill = CreateSkill(matching, "React", 3);
        matching.FreelancerSkills.Add(matchingSkill);

        var excluded = CreateProfile("Nam React", now.AddDays(-1), eloPoints: 1_500);
        excluded.Title = "Frontend Engineer";
        excluded.Availability = 0;
        var excludedSkill = CreateSkill(excluded, "React", 2);
        excluded.FreelancerSkills.Add(excludedSkill);

        context.AddSet(matching, excluded);
        context.AddSet<FreelancerProfilePromotion>();
        context.AddSet<Subscription>();
        context.AddSet(
            CreateReview(matching.UserId, 5),
            CreateReview(excluded.UserId, 3));
        context.AddSet(matchingSkill, excludedSkill);
        context.AddSet<FreelancerProfileCategory>();
        context.AddSet<PlatformSetting>();

        var handler = new GetFreelancersQueryHandler(context, new FixedClock(now));

        var result = await handler.Handle(
            new GetFreelancersQuery(
                Search: "frontend",
                Skills: ["React"],
                AvailabilityStatus: "available",
                MinRating: 4,
                Sort: "rating"),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(matching.FreelancerProfilesId, item.FreelancerProfilesId);
        Assert.Equal(5d, item.Rating);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_MinimumRatingZero_IncludesProfilesWithoutReviews()
    {
        var now = new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var profile = CreateProfile("New Freelancer", now, eloPoints: 500);

        context.AddSet(profile);
        context.AddSet<FreelancerProfilePromotion>();
        context.AddSet<Subscription>();
        context.AddSet<Review>();
        context.AddSet<FreelancerSkill>();
        context.AddSet<FreelancerProfileCategory>();
        context.AddSet<PlatformSetting>();

        var handler = new GetFreelancersQueryHandler(context, new FixedClock(now));

        var result = await handler.Handle(
            new GetFreelancersQuery(MinRating: 0),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(0d, result.Items[0].Rating);
    }

    [Fact]
    public void Validator_RejectsUnboundedOrUnsupportedRequests()
    {
        var validator = new GetFreelancersQueryValidator();

        var result = validator.TestValidate(new GetFreelancersQuery(
            Page: 0,
            PageSize: 51,
            Search: new string('x', 201),
            Skills: Enumerable.Range(0, 21).Select(index => $"skill-{index}").ToList(),
            AvailabilityStatus: "unknown",
            MinRating: 6,
            Sort: "random"));

        result.ShouldHaveValidationErrorFor(query => query.Page);
        result.ShouldHaveValidationErrorFor(query => query.PageSize);
        result.ShouldHaveValidationErrorFor(query => query.Search);
        result.ShouldHaveValidationErrorFor(query => query.Skills);
        result.ShouldHaveValidationErrorFor(query => query.AvailabilityStatus);
        result.ShouldHaveValidationErrorFor(query => query.MinRating);
        result.ShouldHaveValidationErrorFor(query => query.Sort);
    }

    private static FreelancerProfile CreateProfile(
        string fullName,
        DateTime createdAt,
        int eloPoints)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            FullName = fullName,
            Email = $"{userId:N}@example.com",
            IsActive = true,
            CreatedAt = createdAt,
            UserEloScore = new UserEloScore
            {
                UserEloScoresId = Guid.NewGuid(),
                UserId = userId,
                CurrentPoints = eloPoints,
                CreatedAt = createdAt,
                LastActivityAt = createdAt
            }
        };
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            User = user,
            CreatedAt = createdAt
        };
        user.FreelancerProfile = profile;
        user.UserEloScore.User = user;
        return profile;
    }

    private static FreelancerSkill CreateSkill(
        FreelancerProfile profile,
        string name,
        int proficiencyLevel)
    {
        var skillId = Guid.NewGuid();
        return new FreelancerSkill
        {
            FreelancerSkillsId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            Freelancer = profile,
            SkillsId = skillId,
            Skills = new Skill
            {
                SkillsId = skillId,
                Name = name,
                IsActive = true,
                CreatedAt = profile.CreatedAt
            },
            ProficiencyLevel = proficiencyLevel
        };
    }

    private static Review CreateReview(Guid revieweeId, int rating)
    {
        return new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = Guid.NewGuid(),
            ReviewerId = Guid.NewGuid(),
            RevieweeId = revieweeId,
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class FixedClock(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
