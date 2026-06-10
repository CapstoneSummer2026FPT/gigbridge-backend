using Application.Common.Exceptions;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateStatusJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTrue_WhenJobPostBelongsToClient()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var profile = SeedClient(context, userId);
        var jobPost = TestData.JobPost(profile.ClientProfilesId);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var now = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
        var handler = new UpdateStatusJobPostCommandHandler(context, new FixedDateTimeService(now));

        var result = await handler.Handle(
            new UpdateStatusJobPostCommand(jobPost.JobPostsId, userId, new UpdateStatusJobPostRequest { Status = 2 }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, jobPost.Status);
        Assert.Equal(now, jobPost.UpdatedAt);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenClientProfileDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new UpdateStatusJobPostCommandHandler(context, new FixedDateTimeService(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateStatusJobPostCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateStatusJobPostRequest { Status = 2 }),
            CancellationToken.None));
    }

    private static Domain.Entities.ClientProfile SeedClient(DbContext context, Guid userId)
    {
        var profile = TestData.ClientProfile(userId);
        context.Add(TestData.User(userId, UserRole.Client));
        context.Add(profile);
        return profile;
    }
}
