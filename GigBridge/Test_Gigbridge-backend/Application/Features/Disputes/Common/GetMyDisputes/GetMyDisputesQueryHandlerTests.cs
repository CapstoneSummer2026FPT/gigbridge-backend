using Application.Features.Disputes.Common.GetMyDisputes.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Disputes.Common.GetMyDisputes;

public sealed class GetMyDisputesQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ClientSeesOnlyDisputesOnTheirOwnContracts()
    {
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var otherClientUserId = Guid.NewGuid();

        var (ownContract, ownDispute) = BuildContractWithDispute(
            "Website Development", clientUserId: clientUserId, createdAt: Now.AddDays(-1));
        var (otherContract, otherDispute) = BuildContractWithDispute(
            "Mobile App Project", clientUserId: otherClientUserId, createdAt: Now);

        Seed(context, ownContract, ownDispute, otherContract, otherDispute);

        var handler = new GetMyDisputesQueryHandler(context);
        var response = await handler.Handle(new GetMyDisputesQuery(clientUserId), CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal(ownDispute.DisputesId, item.DisputeId);
        Assert.Equal("Website Development", item.ProjectName);
        Assert.Equal(1, response.TotalItems);
    }

    [Fact]
    public async Task Handle_FreelancerSeesOnlyDisputesOnTheirOwnContracts()
    {
        var context = new InMemoryApplicationDbContext();
        var freelancerUserId = Guid.NewGuid();
        var otherFreelancerUserId = Guid.NewGuid();

        var (ownContract, ownDispute) = BuildContractWithDispute(
            "Mobile App Project", freelancerUserId: freelancerUserId, createdAt: Now);
        var (otherContract, otherDispute) = BuildContractWithDispute(
            "Website Development", freelancerUserId: otherFreelancerUserId, createdAt: Now.AddDays(-1));

        Seed(context, ownContract, ownDispute, otherContract, otherDispute);

        var handler = new GetMyDisputesQueryHandler(context);
        var response = await handler.Handle(new GetMyDisputesQuery(freelancerUserId), CancellationToken.None);

        var item = Assert.Single(response.Items);
        Assert.Equal(ownDispute.DisputesId, item.DisputeId);
        Assert.Equal("Mobile App Project", item.ProjectName);
    }

    [Fact]
    public async Task Handle_UserWithNoContracts_ReturnsEmptyPage()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<ClientProfile>();
        context.AddSet<FreelancerProfile>();
        context.AddSet<Dispute>();

        var handler = new GetMyDisputesQueryHandler(context);
        var response = await handler.Handle(new GetMyDisputesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalItems);
    }

    [Fact]
    public async Task Handle_OrdersNewestFirstAndPaginatesCorrectly()
    {
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();

        // Every contract must share the SAME ClientProfile, exactly like a real user only
        // ever has one ClientProfile — otherwise the handler's single-profile lookup only
        // matches whichever one contract happens to carry that profile's id.
        var sharedClientProfile = new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(), UserId = clientUserId, CreatedAt = Now
        };
        var pairs = Enumerable.Range(0, 5)
            .Select(i => BuildContractWithDispute(
                $"Project {i}", clientProfile: sharedClientProfile, createdAt: Now.AddDays(-i)))
            .ToList();
        Seed(context, pairs.SelectMany(p => new object[] { p.Item1, p.Item2 }).ToArray());

        var handler = new GetMyDisputesQueryHandler(context);
        var page1 = await handler.Handle(new GetMyDisputesQuery(clientUserId, Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(new GetMyDisputesQuery(clientUserId, Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(5, page1.TotalItems);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal("Project 0", page1.Items[0].ProjectName); // most recent (createdAt = Now)
        Assert.Equal("Project 1", page1.Items[1].ProjectName);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal("Project 2", page2.Items[0].ProjectName);
    }

    private static void Seed(InMemoryApplicationDbContext context, params object[] entities)
    {
        // AddSet(...) replaces the whole backing set for a type on every call, so every
        // entity of a given type must be passed in a single call — never one call per entity.
        var contracts = entities.OfType<Contract>().ToList();
        context.AddSet(contracts.Select(c => c.JobPosts).ToArray());
        context.AddSet(contracts.Select(c => c.ClientProfiles).ToArray());
        context.AddSet(contracts.Select(c => c.FreelancerProfiles).OfType<FreelancerProfile>().ToArray());
        context.AddSet(contracts.ToArray());
        context.AddSet(entities.OfType<Dispute>().ToArray());
    }

    private static (Contract, Dispute) BuildContractWithDispute(
        string jobPostTitle, Guid? clientUserId = null, Guid? freelancerUserId = null, DateTime createdAt = default,
        ClientProfile? clientProfile = null)
    {
        var jobPost = new JobPost
        {
            JobPostsId = Guid.NewGuid(),
            Title = jobPostTitle,
            Description = "Job description",
            Status = 1,
            CreatedAt = Now
        };
        clientProfile ??= new ClientProfile
        {
            ClientProfilesId = Guid.NewGuid(),
            UserId = clientUserId ?? Guid.NewGuid(),
            CreatedAt = Now
        };
        FreelancerProfile? freelancerProfile = null;
        if (freelancerUserId.HasValue)
        {
            freelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = Guid.NewGuid(),
                UserId = freelancerUserId.Value,
                CreatedAt = Now
            };
        }

        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = jobPost.JobPostsId,
            JobPosts = jobPost,
            ClientProfilesId = clientProfile.ClientProfilesId,
            ClientProfiles = clientProfile,
            FreelancerProfilesId = freelancerProfile?.FreelancerProfilesId,
            FreelancerProfiles = freelancerProfile,
            Title = "Contract title",
            Status = (int)ContractStatus.Active,
            CreatedAt = Now
        };

        var dispute = new Dispute
        {
            DisputesId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            Contracts = contract,
            InitiatorId = clientUserId ?? freelancerUserId ?? Guid.NewGuid(),
            Reason = "Payment dispute",
            Status = (int)DisputeStatus.WaitingAdmin,
            CreatedAt = createdAt == default ? Now : createdAt
        };

        return (contract, dispute);
    }
}
