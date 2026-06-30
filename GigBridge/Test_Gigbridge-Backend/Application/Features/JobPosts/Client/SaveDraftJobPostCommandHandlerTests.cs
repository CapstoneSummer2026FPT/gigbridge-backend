using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class SaveDraftJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenContractDoesNotExist_CreatesPendingFreelancerSelectionContract()
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

        var contract = Assert.Single(contracts.Entities);
        Assert.Equal(jobPostId, contract.JobPostsId);
        Assert.Equal(clientProfileId, contract.ClientProfilesId);
        Assert.Null(contract.FreelancerProfilesId);
        Assert.Null(contract.ProposalsId);
        Assert.Equal("Saved draft", contract.Title);
        Assert.Equal("Draft body", contract.Description);
        Assert.Equal(200m, contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingFreelancerSelection, contract.Status);
        Assert.Equal(now, contract.CreatedAt);
        Assert.Equal(now, contract.UpdatedAt);
        Assert.Equal(now, jobPost.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_WhenContractExists_UpdatesContractWithoutCreatingDuplicate()
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
            Status = (int)ContractStatus.PendingFreelancerSelection,
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
        Assert.Equal("Saved draft", contract.Title);
        Assert.Equal("Draft body", contract.Description);
        Assert.Equal(200m, contract.TotalBudget);
        Assert.Equal(originalCreatedAt, contract.CreatedAt);
        Assert.Equal(now, contract.UpdatedAt);
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
            Location: "Remote",
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
