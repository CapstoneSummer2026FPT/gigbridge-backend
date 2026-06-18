using Application.Common.Interfaces.IService;
using Application.Common.Mappings;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public class UpdateFreelancerProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMissingFreelancerProfileAndCalculatesCompletionScore()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer
        };

        context.AddSet(user);
        var profiles = context.AddSet<FreelancerProfile>();

        var handler = new UpdateFreelancerProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper());

        var result = await handler.Handle(new UpdateFreelancerProfileCommand(CreateValidDto()), CancellationToken.None);

        var profile = Assert.Single(profiles.Entities);
        Assert.Equal(profile.FreelancerProfilesId, result.FreelancerProfilesId);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("Backend Developer", profile.Title);
        Assert.Equal("Experienced .NET developer.", profile.Bio);
        Assert.Equal(0, profile.Availability);
        Assert.Equal("Ho Chi Minh City", profile.Location);
        Assert.Equal(100, profile.ProfileCompletionScore);
        Assert.True(user.IsSetup);
        Assert.NotNull(profile.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_UpdatesExistingFreelancerProfileAndRecalculatesCompletionScore()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            Title = "Old Title",
            Bio = "Old bio",
            Availability = 2,
            Location = "Hanoi",
            ProfileCompletionScore = 40,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var user = new User
        {
            UserId = userId,
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer,
            FreelancerProfile = profile
        };

        context.AddSet(user);
        context.AddSet(profile);

        var handler = new UpdateFreelancerProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper());

        var result = await handler.Handle(new UpdateFreelancerProfileCommand(CreateValidDto()), CancellationToken.None);

        Assert.Equal(profile.FreelancerProfilesId, result.FreelancerProfilesId);
        Assert.Equal("Backend Developer", profile.Title);
        Assert.Equal("Experienced .NET developer.", profile.Bio);
        Assert.Equal(0, profile.Availability);
        Assert.Equal("Ho Chi Minh City", profile.Location);
        Assert.Equal(100, profile.ProfileCompletionScore);
        Assert.True(user.IsSetup);
    }

    private static UpdateFreelancerProfileDto CreateValidDto()
    {
        return new UpdateFreelancerProfileDto
        {
            Title = " Backend Developer ",
            Bio = " Experienced .NET developer. ",
            Availability = 0,
            Location = " Ho Chi Minh City "
        };
    }

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public FixedCurrentUserService(Guid userId)
        {
            UserId = userId.ToString();
        }

        public string? UserId { get; }
        public string? Email => null;
        public string? Role => null;
    }
}
