using Application.Common.Exceptions;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateJobPostCommandHandlerTests
{
    private readonly DateTime _now = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReturnsTrue_WhenRequestIsValid()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clientProfile = SeedClient(context, userId);
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);
        var skillId = Guid.NewGuid();
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var request = CreateValidRequest();
        request.SkillIds = new List<Guid> { skillId, skillId };
        var handler = new UpdateJobPostCommandHandler(context, new FixedDateTimeService(_now));

        var result = await handler.Handle(new UpdateJobPostCommand(jobPost.JobPostsId, userId, request), CancellationToken.None);

        Assert.True(result);
        Assert.Equal("Updated booking module", jobPost.Title);
        Assert.Equal(_now, jobPost.UpdatedAt);
        Assert.Single(await context.JobPostSkills.Where(x => x.JobPostsId == jobPost.JobPostsId).ToListAsync());
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenJobPostDoesNotBelongToClient()
    {
        await using var context = TestDbContextFactory.Create();
        var ownerProfile = SeedClient(context, Guid.NewGuid());
        var otherUserId = Guid.NewGuid();
        SeedClient(context, otherUserId);
        var jobPost = TestData.JobPost(ownerProfile.ClientProfilesId);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new UpdateJobPostCommandHandler(context, new FixedDateTimeService(_now));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateJobPostCommand(jobPost.JobPostsId, otherUserId, CreateValidRequest()),
            CancellationToken.None));
    }

    private static Domain.Entities.ClientProfile SeedClient(DbContext context, Guid userId)
    {
        var profile = TestData.ClientProfile(userId);
        context.Add(TestData.User(userId, UserRole.Client));
        context.Add(profile);
        return profile;
    }

    private static UpdateJobPostRequest CreateValidRequest()
    {
        return new UpdateJobPostRequest
        {
            Title = "Updated booking module",
            Description = "Updated description",
            BudgetType = 0,
            BudgetMin = 500m,
            BudgetMax = 1000m,
            MaxHires = 1,
            ExperienceLevelRequired = 1,
            LocationType = 0,
            ApplicationDeadline = DateTime.UtcNow.AddDays(7)
        };
    }
}
