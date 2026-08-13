using Application.Common.Exceptions;
using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Time;
using Application.Features.Profiles.Common.UpdateUserProfile.Commands;
using Application.Features.Profiles.Common.UpdateUserProfile.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Common;

public sealed class UpdateUserProfileCommandHandlerTests
{
    private static readonly DateTime UpdatedAt = new(2026, 8, 4, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_UpdatesMutableFieldsWithoutChangingIdentityOrRole()
    {
        var context = new InMemoryApplicationDbContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Old Name",
            Email = "old@example.com",
            Avatar = "old-avatar",
            PhoneNumber = "old-phone",
            PreferredLanguage = "vi",
            Role = (int)UserRole.Client,
            IsEmailVerified = true
        };
        context.AddSet(user);
        var handler = CreateHandler(context, user.UserId);

        var result = await handler.Handle(
            new UpdateUserProfileCommand(new UpdateUserProfileDto
            {
                FullName = " New Name ",
                Email = " OLD@example.com ",
                Avatar = " https://cdn.example.com/new.png ",
                PhoneNumber = " +84987654321 ",
                IdentityOrTaxCode = "001 234 567 890",
                PreferredLanguage = " EN "
            }),
            CancellationToken.None);

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal((int)UserRole.Client, result.Role);
        Assert.Equal("New Name", user.FullName);
        Assert.Equal("old@example.com", user.Email);
        Assert.Equal("https://cdn.example.com/new.png", user.Avatar);
        Assert.Equal("+84987654321", user.PhoneNumber);
        Assert.Equal("001234567890", user.IdentityOrTaxCode);
        Assert.Equal("001234567890", result.IdentityOrTaxCode);
        Assert.Equal("en", user.PreferredLanguage);
        Assert.True(user.IsEmailVerified);
        Assert.Equal(UpdatedAt, user.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_ClearsOptionalFieldsAndKeepsVerifiedStatus()
    {
        var context = new InMemoryApplicationDbContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Existing Name",
            Email = "verified@example.com",
            Avatar = "avatar",
            PhoneNumber = "phone",
            IdentityOrTaxCode = "123456789",
            PreferredLanguage = "vi",
            Role = (int)UserRole.Freelancer,
            IsEmailVerified = true
        };
        context.AddSet(user);
        var handler = CreateHandler(context, user.UserId);

        await handler.Handle(
            new UpdateUserProfileCommand(new UpdateUserProfileDto
            {
                FullName = "Existing Name",
                Email = "VERIFIED@example.com",
                Avatar = " ",
                PhoneNumber = null,
                PreferredLanguage = null
            }),
            CancellationToken.None);

        Assert.Null(user.Avatar);
        Assert.Null(user.PhoneNumber);
        Assert.Equal("123456789", user.IdentityOrTaxCode);
        Assert.Null(user.PreferredLanguage);
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task Handle_RejectsEmailChangeThroughProfileUpdates()
    {
        var context = new InMemoryApplicationDbContext();
        var currentUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Current",
            Email = "current@example.com"
        };
        context.AddSet(currentUser);
        var handler = CreateHandler(context, currentUser.UserId);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateUserProfileCommand(new UpdateUserProfileDto
            {
                FullName = "Current",
                Email = "new@example.com"
            }),
            CancellationToken.None));

        Assert.Equal("current@example.com", currentUser.Email);
        Assert.Equal(0, context.SaveChangesCount);
    }

    private static UpdateUserProfileCommandHandler CreateHandler(
        InMemoryApplicationDbContext context,
        Guid userId)
    {
        return new UpdateUserProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            new FixedDateTimeService());
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

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public DateTime UtcNow => UpdatedAt;
    }
}
