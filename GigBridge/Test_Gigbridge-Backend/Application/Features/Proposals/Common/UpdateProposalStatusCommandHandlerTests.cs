using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class UpdateProposalStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_AcceptProposal_AttachesExistingDraftContractAndCreatesEscrow()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var freelancerProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var acceptedProposalId = Guid.NewGuid();
        var otherProposalId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Fixed price job",
            Description = "Build the fixed price workflow.",
            Status = 1,
            CreatedAt = now
        };

        var draftContract = new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = jobPost.Title,
            Description = jobPost.Description,
            TotalBudget = 500m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = now
        };

        var acceptedProposal = new Proposal
        {
            ProposalsId = acceptedProposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = freelancerProfileId,
            ProposedBudget = 1234m,
            Status = 0,
            JobPosts = jobPost
        };

        var otherProposal = new Proposal
        {
            ProposalsId = otherProposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = 900m,
            Status = 1,
            JobPosts = jobPost
        };

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId });
        context.AddSet(jobPost);
        context.AddSet(acceptedProposal, otherProposal);
        context.AddSet(draftContract);
        var escrows = context.AddSet<ContractEscrow>();

        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));
        var command = new UpdateProposalStatusCommand(
            acceptedProposalId,
            clientUserId,
            new UpdateProposalStatusRequest { Status = 2 });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, acceptedProposal.Status);
        Assert.Equal(3, otherProposal.Status);
        Assert.Equal(2, jobPost.Status);
        Assert.Equal(freelancerProfileId, draftContract.FreelancerProfilesId);
        Assert.Equal(acceptedProposalId, draftContract.ProposalsId);
        Assert.Equal(1234m, draftContract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingEscrow, draftContract.Status);

        var escrow = Assert.Single(escrows.Entities);
        Assert.Equal(contractId, escrow.ContractsId);
        Assert.Equal(987.2m, escrow.RequiredAmount);
        Assert.Equal(0m, escrow.FundedAmount);
        Assert.Equal(0.8m, escrow.RequiredPercentage);
        Assert.Equal("VND", escrow.Currency);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, escrow.Status);
        Assert.Equal(now, escrow.CreatedAt);
    }

    [Fact]
    public async Task Handle_AcceptProposalWithoutBudget_ThrowsBadRequest()
    {
        var now = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var jobPost = new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = "Fixed price job",
            Description = "Build the fixed price workflow.",
            Status = 1,
            CreatedAt = now
        };

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = clientUserId });
        context.AddSet(jobPost);
        context.AddSet(new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = jobPostId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = null,
            Status = 0,
            JobPosts = jobPost
        });
        context.AddSet(new Contract
        {
            ContractsId = Guid.NewGuid(),
            JobPostsId = jobPostId,
            ClientProfilesId = clientProfileId,
            Title = jobPost.Title,
            TotalBudget = 0m,
            Status = (int)ContractStatus.PendingFreelancerSelection,
            CreatedAt = now
        });
        context.AddSet<ContractEscrow>();

        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(now));
        var command = new UpdateProposalStatusCommand(
            proposalId,
            clientUserId,
            new UpdateProposalStatusRequest { Status = 2 });

        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(command, CancellationToken.None));
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
