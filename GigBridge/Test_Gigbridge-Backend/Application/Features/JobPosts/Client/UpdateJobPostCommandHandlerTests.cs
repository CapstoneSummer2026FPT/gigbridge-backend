using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Domain.Entities;
using Infrastructure.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesVisibility()
    {
        var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Old title",
            Description = "Old description",
            Status = 0,
            Visibility = 0,
            CreatedAt = now.AddDays(-1)
        });
        context.AddSet<JobPostSkill>();

        var request = new UpdateJobPostRequest(
            Title: "Updated title",
            Description: "Updated description",
            MajorCategoryId: null,
            BudgetMin: 100m,
            BudgetMax: 200m,
            Currency: "VND",
            EstimatedDuration: "1 week",
            Location: "Remote",
            Visibility: 2,
            EndDate: now.AddDays(7),
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new UpdateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        var result = await handler.Handle(
            new UpdateJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        var jobPost = Assert.Single(context.Set<JobPost>());
        Assert.True(result);
        Assert.Equal(2, jobPost.Visibility);
        Assert.Equal(now, jobPost.UpdatedAt);
    }

    [Fact]
    public async Task Handle_WithUnsafeContent_ThrowsValidationExceptionAndDoesNotUpdate()
    {
        var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Old title",
            Description = "Old description",
            Status = 0,
            Visibility = 0,
            CreatedAt = now.AddDays(-1)
        };
        context.AddSet(jobPost);
        context.AddSet<JobPostSkill>();

        var request = new UpdateJobPostRequest(
            Title: "Buôn ma tuy",
            Description: "Tuyen nguoi van chuyen hang",
            MajorCategoryId: null,
            BudgetMin: 100m,
            BudgetMax: 200m,
            Currency: "VND",
            EstimatedDuration: "1 week",
            Location: "Remote",
            Visibility: 2,
            EndDate: now.AddDays(7),
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new UpdateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new UpdateJobPostCommand(jobPostId, userId, request), CancellationToken.None));

        Assert.Contains(
            "Job post appears to request or promote illegal drug-related work.",
            exception.Errors["JobPostContent"]);
        Assert.Equal("Old title", jobPost.Title);
        Assert.Equal("Old description", jobPost.Description);
        Assert.Equal(0, jobPost.Visibility);
        Assert.Null(jobPost.UpdatedAt);
        Assert.Equal(0, context.SaveChangesCount);
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
