using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Profiles.Common.GetMyUserProfile.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Common;

public sealed class GetMyUserProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPrivateFieldsForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        var user = new User
        {
            UserId = userId,
            FullName = "Current User",
            Email = "current@example.com",
            Avatar = "avatar.png",
            PhoneNumber = "+84901234567",
            PreferredLanguage = "vi",
            Role = (int)UserRole.Client
        };
        context.AddSet(user);
        var handler = new GetMyUserProfileQueryHandler(
            context,
            new FixedCurrentUserService(userId.ToString()));

        var result = await handler.Handle(new GetMyUserProfileQuery(), CancellationToken.None);

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Avatar, result.Avatar);
        Assert.Equal(user.PhoneNumber, result.PhoneNumber);
        Assert.Equal(user.PreferredLanguage, result.PreferredLanguage);
        Assert.Equal(user.Role, result.Role);
    }

    [Fact]
    public async Task Handle_ThrowsBadRequestForInvalidTokenUserId()
    {
        var context = new InMemoryApplicationDbContext();
        var handler = new GetMyUserProfileQueryHandler(
            context,
            new FixedCurrentUserService("not-a-guid"));

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new GetMyUserProfileQuery(),
            CancellationToken.None));
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public FixedCurrentUserService(string? userId)
        {
            UserId = userId;
        }

        public string? UserId { get; }
        public string? Email => null;
        public string? Role => null;
    }
}
