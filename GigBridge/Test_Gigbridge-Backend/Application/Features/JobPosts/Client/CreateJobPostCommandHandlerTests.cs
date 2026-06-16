using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Domain.Entities;
using Domain.Enums;
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
            CategoryId: null,
            BudgetMin: 700m,
            BudgetMax: 1200m,
            Currency: "VND",
            EstimatedDuration: "2 weeks",
            MaxHires: 1,
            Location: "Remote",
            Visibility: 0,
            EndDate: endDate,
            SkillIds: new List<Guid>());

        var handler = new CreateJobPostCommandHandler(context, new FixedDateTimeService(now));
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
            CategoryId: null,
            BudgetMin: null,
            BudgetMax: 1200m,
            Currency: "VND",
            EstimatedDuration: null,
            MaxHires: null,
            Location: null,
            Visibility: null,
            EndDate: null,
            SkillIds: new List<Guid>());

        var handler = new CreateJobPostCommandHandler(context, new FixedDateTimeService(now));
        await handler.Handle(new CreateJobPostCommand(request, userId), CancellationToken.None);

        var contract = Assert.Single(contracts.Entities);
        Assert.Equal(1200m, contract.TotalBudget);
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
