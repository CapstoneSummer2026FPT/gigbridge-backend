using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Domain.Entities;
using Infrastructure.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class SaveDraftJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithUnsafeDraftContent_ThrowsValidationExceptionAndDoesNotUpdate()
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
            Title = "Safe draft",
            Description = "Safe draft description",
            Status = 0,
            Visibility = 0,
            CreatedAt = now.AddDays(-1)
        };
        context.AddSet(jobPost);
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();

        var request = new SaveDraftJobPostRequest(
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
            IsAigenerated: false,
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>(),
            Questions: null);

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new SaveDraftJobPostCommand(jobPostId, userId, request), CancellationToken.None));

        Assert.Contains(
            "Job post appears to request or promote illegal drug-related work.",
            exception.Errors["JobPostContent"]);
        Assert.Equal("Safe draft", jobPost.Title);
        Assert.Equal("Safe draft description", jobPost.Description);
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
