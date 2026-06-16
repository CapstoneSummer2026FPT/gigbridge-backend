using Application.Common.Exceptions;
using Application.Features.Contracts.Common.GetContractByJobPost.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class GetContractByJobPostQueryHandlerTests
{
    [Fact]
    public async Task Handle_OwnerClientCanViewDraftContract()
    {
        var fixture = ContractQueryFixture.Create(attachedFreelancer: false);
        var handler = new GetContractByJobPostQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetContractByJobPostQuery(fixture.JobPostId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(fixture.ContractId, result.ContractId);
        Assert.Equal(fixture.JobPostId, result.JobPostId);
        Assert.Equal(fixture.ClientProfileId, result.ClientProfileId);
        Assert.Null(result.FreelancerProfileId);
        Assert.Equal("Draft contract", result.Title);
        Assert.Equal(500m, result.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingFreelancerSelection, result.Status);
        Assert.Null(result.Escrow);
    }

    [Fact]
    public async Task Handle_AttachedFreelancerCanViewContractWithEscrow()
    {
        var fixture = ContractQueryFixture.Create(attachedFreelancer: true);
        var handler = new GetContractByJobPostQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetContractByJobPostQuery(fixture.JobPostId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(fixture.FreelancerProfileId, result.FreelancerProfileId);
        Assert.NotNull(result.Escrow);
        Assert.Equal(400m, result.Escrow.RequiredAmount);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, result.Escrow.Status);
    }

    [Fact]
    public async Task Handle_UnrelatedFreelancerCannotViewContract()
    {
        var fixture = ContractQueryFixture.Create(attachedFreelancer: true);
        var unrelatedUserId = Guid.NewGuid();
        fixture.Context.Set<User>().Add(new User
        {
            UserId = unrelatedUserId,
            FullName = "Unrelated Freelancer",
            Email = "unrelated@example.com",
            Role = (int)UserRole.Freelancer
        });
        fixture.Context.Set<FreelancerProfile>().Add(new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = unrelatedUserId
        });

        var handler = new GetContractByJobPostQueryHandler(fixture.Context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(
                new GetContractByJobPostQuery(fixture.JobPostId, unrelatedUserId),
                CancellationToken.None));
    }

    private sealed class ContractQueryFixture
    {
        public InMemoryApplicationDbContext Context { get; private init; } = null!;

        public Guid ClientUserId { get; private init; }

        public Guid FreelancerUserId { get; private init; }

        public Guid ClientProfileId { get; private init; }

        public Guid FreelancerProfileId { get; private init; }

        public Guid JobPostId { get; private init; }

        public Guid ContractId { get; private init; }

        public static ContractQueryFixture Create(bool attachedFreelancer)
        {
            var context = new InMemoryApplicationDbContext();
            var clientUserId = Guid.NewGuid();
            var freelancerUserId = Guid.NewGuid();
            var clientProfileId = Guid.NewGuid();
            var freelancerProfileId = Guid.NewGuid();
            var jobPostId = Guid.NewGuid();
            var contractId = Guid.NewGuid();

            context.AddSet(
                new User
                {
                    UserId = clientUserId,
                    FullName = "Client",
                    Email = "client@example.com",
                    Role = (int)UserRole.Client
                },
                new User
                {
                    UserId = freelancerUserId,
                    FullName = "Freelancer",
                    Email = "freelancer@example.com",
                    Role = (int)UserRole.Freelancer
                });

            context.AddSet(new ClientProfile
            {
                ClientProfilesId = clientProfileId,
                UserId = clientUserId
            });

            context.AddSet(new FreelancerProfile
            {
                FreelancerProfilesId = freelancerProfileId,
                UserId = freelancerUserId
            });

            context.AddSet(new JobPost
            {
                JobPostsId = jobPostId,
                ClientProfilesId = clientProfileId,
                Title = "Draft contract",
                Description = "Contract draft body",
                Status = 1,
                CreatedAt = DateTime.UtcNow
            });

            var contract = new Contract
            {
                ContractsId = contractId,
                JobPostsId = jobPostId,
                ClientProfilesId = clientProfileId,
                FreelancerProfilesId = attachedFreelancer ? freelancerProfileId : null,
                Title = "Draft contract",
                Description = "Contract draft body",
                TotalBudget = 500m,
                Status = attachedFreelancer
                    ? (int)ContractStatus.PendingEscrow
                    : (int)ContractStatus.PendingFreelancerSelection,
                CreatedAt = DateTime.UtcNow
            };

            context.AddSet(contract);
            if (attachedFreelancer)
            {
                context.AddSet(new ContractEscrow
                {
                    ContractEscrowId = Guid.NewGuid(),
                    ContractsId = contractId,
                    Contract = contract,
                    RequiredAmount = 400m,
                    FundedAmount = 0m,
                    RequiredPercentage = 0.8m,
                    Currency = "VND",
                    Status = (int)ContractEscrowStatus.PendingFunding,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                context.AddSet<ContractEscrow>();
            }

            return new ContractQueryFixture
            {
                Context = context,
                ClientUserId = clientUserId,
                FreelancerUserId = freelancerUserId,
                ClientProfileId = clientProfileId,
                FreelancerProfileId = freelancerProfileId,
                JobPostId = jobPostId,
                ContractId = contractId
            };
        }
    }
}
