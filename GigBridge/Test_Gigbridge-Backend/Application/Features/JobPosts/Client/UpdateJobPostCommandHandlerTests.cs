using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Domain.Entities;
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
            MaxHires: 1,
            Location: "Remote",
            Visibility: 2,
            EndDate: now.AddDays(7),
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new UpdateJobPostCommandHandler(context, new FixedDateTimeService(now));

        var result = await handler.Handle(
            new UpdateJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        var jobPost = Assert.Single(context.Set<JobPost>());
        Assert.True(result);
        Assert.Equal(2, jobPost.Visibility);
        Assert.Equal(now, jobPost.UpdatedAt);
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
