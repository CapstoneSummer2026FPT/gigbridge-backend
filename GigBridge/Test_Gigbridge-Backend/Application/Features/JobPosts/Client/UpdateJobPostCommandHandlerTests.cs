using Domain.Enums.AiInterviews;
using Application.Common.Exceptions;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Time;
using Application.Common.Models.Ai;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Domain.Entities;

using Application.Common.InternalServices.JobPosts.Services;
using NSubstitute;
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

    [Fact]
    public async Task Handle_UpdatesAiInterviewDefinition_WhenActiveInterviewExists()
    {
        var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var skillId = Guid.NewGuid();

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
        context.AddSet(new Skill
        {
            SkillsId = skillId,
            Name = "System Skill"
        });
        context.AddSet(new AiInterviewDefinition
        {
            AiInterviewDefinitionsId = Guid.NewGuid(),
            JobPostId = jobPostId,
            ClientUserId = userId,
            Language = "auto",
            Mode = "voice",
            QuestionCount = 5,
            Status = AiInterviewDefinitionStatus.Active,
            ExternalReference = "old-aidef-ref",
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
            Visibility: 2,
            EndDate: now.AddDays(7),
            SkillIds: new List<Guid> { skillId },
            CustomSkillNames: new List<string> { "Custom Skill" });

        var aiServiceClient = Substitute.For<IAiServiceClient>();
        aiServiceClient.CreateInterviewDefinitionAsync(
            Arg.Any<AiInterviewDefinitionRequestDto>(),
            Arg.Any<CancellationToken>())
            .Returns(new AiInterviewDefinitionResponseDto
            {
                DefinitionReference = "new-aidef-ref",
                Mode = "voice",
                Language = "auto",
                QuestionCount = 5
            });

        var handler = new UpdateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService(),
            aiServiceClient);

        var result = await handler.Handle(
            new UpdateJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        Assert.True(result);

        // Verify the AI Service was called with updated details
        await aiServiceClient.Received(1).CreateInterviewDefinitionAsync(
            Arg.Is<AiInterviewDefinitionRequestDto>(req =>
                req.JobId == jobPostId.ToString() &&
                req.JobTitle == "Updated title" &&
                req.JobDescription == "Updated description" &&
                req.JobSkills.Contains("System Skill") &&
                req.JobSkills.Contains("Custom Skill") &&
                req.Mode == "voice" &&
                req.Language == "auto" &&
                req.QuestionCount == 5),
            Arg.Any<CancellationToken>());

        // Verify definition in DB was updated
        var definition = Assert.Single(context.Set<AiInterviewDefinition>());
        Assert.Equal("new-aidef-ref", definition.ExternalReference);
        Assert.Equal(now, definition.UpdatedAt);
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
