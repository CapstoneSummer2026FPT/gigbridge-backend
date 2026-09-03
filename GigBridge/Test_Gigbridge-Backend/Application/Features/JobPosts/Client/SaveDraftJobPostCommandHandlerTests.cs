using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using Application.Common.InternalServices.JobPosts.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class SaveDraftJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenContractDoesNotExist_DoesNotCreateContract()
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
            Title = "Untitled Job Post",
            Description = string.Empty,
            Status = 0,
            Visibility = 0,
            CreatedAt = now.AddDays(-1)
        };
        context.AddSet(jobPost);
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();
        var contracts = context.AddSet<Contract>();

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        await handler.Handle(
            new SaveDraftJobPostCommand(jobPostId, userId, CreateValidRequest(now)),
            CancellationToken.None);

        Assert.Empty(contracts.Entities);
        Assert.Equal("Saved draft", jobPost.Title);
        Assert.Equal("Draft body", jobPost.Description);
        Assert.Equal(now, jobPost.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_WithIncompleteMilestoneDraft_PreservesNullableDeadline()
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
            Title = "Untitled Job Post",
            Description = string.Empty,
            Status = 0,
            CreatedAt = now
        });
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();
        var milestoneSet = context.AddSet<JobPostMilestonePlan>();

        var request = CreateValidRequest(now) with
        {
            MilestonePlans =
            [
                new JobPostMilestonePlanDto
                {
                    Title = "Draft milestone",
                    Amount = 100m,
                    EstimatedDuration = "2 months",
                    DueDate = null,
                    OrderIndex = 0
                }
            ]
        };

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        await handler.Handle(
            new SaveDraftJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        var milestone = Assert.Single(milestoneSet.Entities);
        Assert.Equal("2 months", milestone.EstimatedDuration);
        Assert.Null(milestone.DueDate);
    }

    [Fact]
    public async Task Handle_WithValidWorkItemPlan_PersistsWorkItemsTiedToTheirMilestone()
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
            Title = "Untitled Job Post",
            Description = string.Empty,
            Status = 0,
            CreatedAt = now
        });
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();
        var milestoneSet = context.AddSet<JobPostMilestonePlan>();

        var request = CreateValidRequest(now) with
        {
            MilestonePlans =
            [
                new JobPostMilestonePlanDto
                {
                    Title = "Backend milestone",
                    Amount = 100m,
                    EstimatedDuration = "2 weeks",
                    OrderIndex = 0,
                    WorkItems =
                    [
                        new JobPostWorkItemDto { Title = "Database design", EstimatedDuration = "2 days", OrderIndex = 0 },
                        new JobPostWorkItemDto { Title = "API development", EstimatedDuration = "4 days", OrderIndex = 1 },
                    ]
                }
            ]
        };

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        await handler.Handle(
            new SaveDraftJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        var milestone = Assert.Single(milestoneSet.Entities);
        Assert.Equal(2, milestone.WorkItems.Count);
        Assert.All(milestone.WorkItems, item => Assert.Equal(milestone.JobPostMilestonePlanId, item.JobPostMilestonePlanId));
        Assert.Contains(milestone.WorkItems, item => item.Title == "Database design" && item.EstimatedDuration == "2 days");
        Assert.Contains(milestone.WorkItems, item => item.Title == "API development" && item.EstimatedDuration == "4 days");
    }

    [Fact]
    public async Task Handle_WithEmptyMilestonePlan_PreservesExpectedBudget()
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
            Title = "Untitled Job Post",
            Description = string.Empty,
            Status = 0,
            Visibility = 0,
            CreatedAt = now
        };
        context.AddSet(jobPost);
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();
        context.AddSet<JobPostMilestonePlan>();

        var request = CreateValidRequest(now) with
        {
            BudgetMin = 1000m,
            BudgetMax = 1000m,
            MilestonePlans = []
        };

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        await handler.Handle(
            new SaveDraftJobPostCommand(jobPostId, userId, request),
            CancellationToken.None);

        Assert.Equal(1000m, jobPost.BudgetMin);
        Assert.Equal(1000m, jobPost.BudgetMax);
    }

    [Fact]
    public async Task Handle_WhenContractExists_DoesNotUpdateContractOrCreateDuplicate()
    {
        var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var originalCreatedAt = now.AddDays(-3);

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        context.AddSet(new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Old title",
            Description = "Old body",
            BudgetMax = 50m,
            Status = 0,
            Visibility = 0,
            CreatedAt = now.AddDays(-2)
        });
        context.AddSet<JobPostSkill>();
        context.AddSet<JobPostQuestion>();
        var contracts = context.AddSet(new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Old contract",
            Description = "Old contract body",
            TotalBudget = 50m,
            Status = 0,
            CreatedAt = originalCreatedAt
        });

        var handler = new SaveDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        await handler.Handle(
            new SaveDraftJobPostCommand(jobPostId, userId, CreateValidRequest(now)),
            CancellationToken.None);

        var contract = Assert.Single(contracts.Entities);
        Assert.Equal(contractId, contract.ContractsId);
        Assert.Equal("Old contract", contract.Title);
        Assert.Equal("Old contract body", contract.Description);
        Assert.Equal(50m, contract.TotalBudget);
        Assert.Equal(originalCreatedAt, contract.CreatedAt);
        Assert.Null(contract.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

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

    private static SaveDraftJobPostRequest CreateValidRequest(DateTime now)
    {
        return new SaveDraftJobPostRequest(
            Title: " Saved draft ",
            Description: " Draft body ",
            MajorCategoryId: null,
            BudgetMin: 100m,
            BudgetMax: 200m,
            Currency: "VND",
            EstimatedDuration: "1 week",
            Visibility: 2,
            EndDate: now.AddDays(7),
            IsAigenerated: false,
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>(),
            Questions: null);
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
