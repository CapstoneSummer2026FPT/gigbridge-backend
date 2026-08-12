using Application.Common.Exceptions;
using Application.Features.Profiles.Common.GetUserProfile.Queries;
using Domain.Entities;
using Domain.Enums.Accounts;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Common;

public sealed class GetUserProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyRequestedPublicUserFields()
    {
        var context = new InMemoryApplicationDbContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Profile User",
            Email = "profile@example.com",
            Avatar = "https://cdn.example.com/avatar.png",
            PhoneNumber = "+84901234567",
            PreferredLanguage = "vi",
            Role = (int)UserRole.Freelancer
        };
        context.AddSet(user);
        var handler = new GetUserProfileQueryHandler(context);

        var result = await handler.Handle(
            new GetUserProfileQuery(user.UserId),
            CancellationToken.None);

        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Avatar, result.Avatar);
        Assert.Equal(user.Role, result.Role);
        Assert.False(result.IsPremium);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundWhenUserDoesNotExist()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<User>();
        var handler = new GetUserProfileQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetUserProfileQuery(Guid.NewGuid()),
            CancellationToken.None));
    }
}
