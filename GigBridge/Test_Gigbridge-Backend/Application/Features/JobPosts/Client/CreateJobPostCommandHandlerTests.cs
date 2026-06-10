using Application.Common.Exceptions;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class CreateJobPostCommandHandlerTests
{
    private readonly DateTime _now = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReturnsJobPostId_WhenRequestIsValid()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clientProfile = TestData.ClientProfile(userId);
        context.Users.Add(TestData.User(userId, UserRole.Client));
        context.ClientProfiles.Add(clientProfile);
        await context.SaveChangesAsync();
        var skillId = Guid.NewGuid();
        var handler = new CreateJobPostCommandHandler(context, new FixedDateTimeService(_now));

        var jobPostId = await handler.Handle(
            new CreateJobPostCommand(CreateValidRequest(skillId, skillId), userId),
            CancellationToken.None);

        var jobPost = await context.JobPosts.Include(x => x.JobPostSkills).SingleAsync(x => x.JobPostsId == jobPostId);
        Assert.Equal(clientProfile.ClientProfilesId, jobPost.ClientProfilesId);
        Assert.Equal("Build a booking module", jobPost.Title);
        Assert.Equal(1, jobPost.Status);
        Assert.Equal(_now, jobPost.CreatedAt);
        Assert.Single(jobPost.JobPostSkills);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenClientProfileDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new CreateJobPostCommandHandler(context, new FixedDateTimeService(_now));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateJobPostCommand(CreateValidRequest(), Guid.NewGuid()),
            CancellationToken.None));
    }

    private static CreateJobPostRequest CreateValidRequest(params Guid[] skillIds)
    {
        return new CreateJobPostRequest(
            Title: "Build a booking module",
            Description: "Create booking workflow and notification logic.",
            CategoryId: Guid.NewGuid(),
            BudgetType: 0,
            BudgetMin: 500m,
            BudgetMax: 1000m,
            Currency: "VND",
            EstimatedDuration: "2 weeks",
            MaxHires: 1,
            ExperienceLevelRequired: 1,
            LocationType: 0,
            Location: "Remote",
            Visibility: 1,
            ApplicationDeadline: DateTime.UtcNow.AddDays(7),
            SkillIds: skillIds.ToList());
    }
}
