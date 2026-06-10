using Application.Common.Exceptions;
using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class GetMyJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsClientJobPosts_WhenClientProfileExists()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var profile = TestData.ClientProfile(userId);
        context.Users.Add(TestData.User(userId, UserRole.Client));
        context.ClientProfiles.Add(profile);
        context.JobPosts.Add(TestData.JobPost(profile.ClientProfilesId));
        await context.SaveChangesAsync();
        var handler = new GetMyJobPostsQueryHandler(context);

        var result = await handler.Handle(new GetMyJobPostsQuery { UserId = userId }, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenClientProfileDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetMyJobPostsQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetMyJobPostsQuery { UserId = Guid.NewGuid() },
            CancellationToken.None));
    }
}
