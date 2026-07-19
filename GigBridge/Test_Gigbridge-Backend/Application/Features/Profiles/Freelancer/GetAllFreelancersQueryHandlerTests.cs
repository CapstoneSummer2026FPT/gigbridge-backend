using Application.Common.Interfaces.IService;
using Application.Features.Profiles.FreelancerProfile.GetAllFreelancers.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public class GetAllFreelancersQueryHandlerTests
{
    [Fact]
    public async Task Handle_OrdersActivePromotionByProfileId_WhenNavigationIsNotLoaded()
    {
        var now = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var regularProfile = CreateProfile("Regular Freelancer", now);
        var promotedProfile = CreateProfile("Promoted Freelancer", now.AddDays(-1));

        context.AddSet(regularProfile, promotedProfile);
        context.AddSet(new FreelancerProfilePromotion
        {
            FreelancerProfilePromotionsId = Guid.NewGuid(),
            FreelancerProfileId = promotedProfile.FreelancerProfilesId,
            BoostWeight = 10m,
            Status = PromotionStatus.Active,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1),
            CreatedAt = now.AddHours(-1)
        });

        var handler = new GetAllFreelancersQueryHandler(context, new FixedClock(now));

        var result = (await handler.Handle(new GetAllFreelancersQuery(), CancellationToken.None)).ToList();

        Assert.Equal(promotedProfile.FreelancerProfilesId, result[0].FreelancerProfilesId);
        Assert.Equal(regularProfile.FreelancerProfilesId, result[1].FreelancerProfilesId);
    }

    private static FreelancerProfile CreateProfile(string fullName, DateTime createdAt)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            FullName = fullName,
            Email = $"{userId:N}@example.com",
            IsActive = true,
            CreatedAt = createdAt
        };
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            User = user,
            CreatedAt = createdAt
        };
        user.FreelancerProfile = profile;
        return profile;
    }

    private sealed class FixedClock(DateTime now) : IDateTimeService
    {
        public DateTime UtcNow { get; } = now;
    }
}
