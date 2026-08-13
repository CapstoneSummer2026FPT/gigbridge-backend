using Application.Common.Interfaces.Identity;
using Application.Common.Mappings;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.Commands;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Client;

public class UpdateClientProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMissingClientProfileAndMarksSetupComplete()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            FullName = "Client User",
            Email = "client@example.com",
            Role = (int)UserRole.Client
        };

        context.AddSet(user);
        var profiles = context.AddSet<ClientProfile>();

        var handler = new UpdateClientProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper());

        var result = await handler.Handle(new UpdateClientProfileCommand(CreateValidDto()), CancellationToken.None);

        var profile = Assert.Single(profiles.Entities);
        Assert.Equal(profile.ClientProfilesId, result.ClientProfilesId);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("Acme Labs", profile.CompanyName);
        Assert.Equal("Technology", profile.Industry);
        Assert.Equal("Ho Chi Minh City", profile.Location);
        Assert.True(user.IsSetup);
        Assert.NotNull(profile.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_UpdatesExistingClientProfile()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = userId,
            CompanyName = "Old Company",
            Industry = "Finance",
            Location = "Hanoi",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var user = new User
        {
            UserId = userId,
            FullName = "Client User",
            Email = "client@example.com",
            Role = (int)UserRole.Client,
            ClientProfile = profile
        };

        context.AddSet(user);
        context.AddSet(profile);

        var handler = new UpdateClientProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper());

        var result = await handler.Handle(new UpdateClientProfileCommand(CreateValidDto()), CancellationToken.None);

        Assert.Equal(profile.ClientProfilesId, result.ClientProfilesId);
        Assert.Equal("Acme Labs", profile.CompanyName);
        Assert.Equal("https://acme.example", profile.CompanyWebsite);
        Assert.Equal(1, profile.CompanySize);
        Assert.Equal("Building reliable SaaS tools.", profile.CompanyDescription);
        Assert.True(user.IsSetup);
    }

    private static UpdateClientProfileDto CreateValidDto()
    {
        return new UpdateClientProfileDto
        {
            CompanyName = " Acme Labs ",
            CompanyWebsite = " https://acme.example ",
            CompanySize = 1,
            Industry = " Technology ",
            CompanyDescription = " Building reliable SaaS tools. ",
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
