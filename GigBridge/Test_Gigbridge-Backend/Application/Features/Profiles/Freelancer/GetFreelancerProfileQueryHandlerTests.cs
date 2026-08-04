using Application.Common.Interfaces.IService;
using Application.Features.Premium.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public sealed class GetFreelancerProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEnrichedPortfolioItems()
    {
        await using var context = new GigbridgeDbContext(
            new DbContextOptionsBuilder<GigbridgeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            UserId = userId,
            FullName = "Portfolio Freelancer",
            Email = "portfolio@example.com",
            Role = (int)UserRole.Freelancer,
            FreelancerProfile = profile
        };
        profile.User = user;
        profile.PortfolioItems.Add(new PortfolioItem
        {
            PortfolioItemsId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            Freelancer = profile,
            Title = "GigBridge profile",
            Description = "A profile redesign project.",
            ProjectUrl = "https://example.com/project",
            ImageUrl = "https://example.com/project.png",
            ProjectDate = new DateOnly(2026, 8, 1)
        });
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var premiumAccess = Substitute.For<IPremiumAccessService>();
        premiumAccess.GetPremiumBenefitsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new PremiumBenefitsDto(false, false, false, null, null));
        var result = await new GetFreelancerProfileQueryHandler(context, premiumAccess)
            .Handle(new GetFreelancerProfileQuery(userId), CancellationToken.None);

        var portfolioItem = Assert.Single(result.PortfolioItems);
        Assert.Equal("GigBridge profile", portfolioItem.Title);
        Assert.Equal("A profile redesign project.", portfolioItem.Description);
        Assert.Equal("https://example.com/project", portfolioItem.ProjectUrl);
        Assert.Equal("https://example.com/project.png", portfolioItem.ImageUrl);
        Assert.Equal("2026-08-01", portfolioItem.ProjectDate);
    }
}
