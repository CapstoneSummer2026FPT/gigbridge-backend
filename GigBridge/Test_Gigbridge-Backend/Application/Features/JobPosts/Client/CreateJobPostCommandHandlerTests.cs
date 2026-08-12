using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Domain.Entities;
using Domain.Enums.Contracts;
using Infrastructure.Services.ContentModerationService;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class CreateJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesJobPostAndPendingFreelancerSelectionContract()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var endDate = now.AddDays(10);

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        var jobPosts = context.AddSet<JobPost>();
        var contracts = context.AddSet<Contract>();

        var request = new CreateJobPostRequest(
            Title: " Build contract draft ",
            Description: " Prepare escrow workflow ",
            MajorCategoryId: null,
            BudgetMin: 700m,
            BudgetMax: 1200m,
            Currency: "VND",
            EstimatedDuration: "2 weeks",
            Visibility: 0,
            EndDate: endDate,
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new CreateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());
        var jobPostId = await handler.Handle(new CreateJobPostCommand(request, userId), CancellationToken.None);

        var jobPost = Assert.Single(jobPosts.Entities);
        Assert.Equal(jobPost.JobPostsId, jobPostId);

        var contract = Assert.Single(contracts.Entities);
        Assert.Equal(jobPostId, contract.JobPostsId);
        Assert.Equal(clientProfileId, contract.ClientProfilesId);
        Assert.Null(contract.FreelancerProfilesId);
        Assert.Null(contract.ProposalsId);
        Assert.Equal("Build contract draft", contract.Title);
        Assert.Equal("Prepare escrow workflow", contract.Description);
        Assert.Equal(700m, contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingFreelancerSelection, contract.Status);
        Assert.Equal(DateOnly.FromDateTime(endDate), contract.EndDate);
        Assert.Equal(now, contract.CreatedAt);
    }

    [Fact]
    public async Task Handle_CreatesDraftContractWithBudgetMaxWhenBudgetMinMissing()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();

        context.AddSet(new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = userId });
        context.AddSet<JobPost>();
        var contracts = context.AddSet<Contract>();

        var request = new CreateJobPostRequest(
            Title: "Build contract draft",
            Description: "Prepare escrow workflow",
            MajorCategoryId: null,
            BudgetMin: null,
            BudgetMax: 1200m,
            Currency: "VND",
            EstimatedDuration: null,
            Visibility: null,
            EndDate: null,
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new CreateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());
        await handler.Handle(new CreateJobPostCommand(request, userId), CancellationToken.None);

        var contract = Assert.Single(contracts.Entities);
        Assert.Equal(1200m, contract.TotalBudget);
    }

    [Fact]
    public async Task Handle_WithUnsafeContent_ThrowsValidationExceptionAndDoesNotSave()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var jobPosts = context.AddSet<JobPost>();
        var contracts = context.AddSet<Contract>();

        var request = new CreateJobPostRequest(
            Title: "Buôn ma tuy",
            Description: "Tuyen nguoi van chuyen hang",
            MajorCategoryId: null,
            BudgetMin: null,
            BudgetMax: 1200m,
            Currency: "VND",
            EstimatedDuration: null,
            Visibility: null,
            EndDate: null,
            SkillIds: new List<Guid>(),
            CustomSkillNames: new List<string>());

        var handler = new CreateJobPostCommandHandler(
            context,
            new FixedDateTimeService(now),
            new ContentModerationService());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new CreateJobPostCommand(request, userId), CancellationToken.None));

        Assert.Contains(
            "Job post appears to request or promote illegal drug-related work.",
            exception.Errors["JobPostContent"]);
        Assert.Empty(jobPosts.Entities);
        Assert.Empty(contracts.Entities);
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
