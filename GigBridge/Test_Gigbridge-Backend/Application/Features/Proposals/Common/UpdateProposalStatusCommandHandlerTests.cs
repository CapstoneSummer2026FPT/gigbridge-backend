using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.DTOs;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class UpdateProposalStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_AcceptsProposalAndRejectsCompetingProposals_WhenUserIsClientOwner()
    {
        await using var context = TestDbContextFactory.Create();
        var clientUserId = Guid.NewGuid();
        var clientProfile = TestData.ClientProfile(clientUserId);
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);
        var freelancerOne = TestData.FreelancerProfile(Guid.NewGuid());
        var freelancerTwo = TestData.FreelancerProfile(Guid.NewGuid());
        var accepted = TestData.Proposal(jobPost.JobPostsId, freelancerOne.FreelancerProfilesId);
        var competing = TestData.Proposal(jobPost.JobPostsId, freelancerTwo.FreelancerProfilesId);
        context.Users.Add(TestData.User(clientUserId, UserRole.Client));
        context.ClientProfiles.Add(clientProfile);
        context.JobPosts.Add(jobPost);
        context.FreelancerProfiles.AddRange(freelancerOne, freelancerTwo);
        context.Proposals.AddRange(accepted, competing);
        await context.SaveChangesAsync();
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(DateTime.UtcNow));

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(accepted.ProposalsId, clientUserId, new UpdateProposalStatusRequest { Status = 2 }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, accepted.Status);
        Assert.Equal(3, competing.Status);
        Assert.Equal(2, jobPost.Status);
    }

    [Fact]
    public async Task Handle_WithdrawsProposal_WhenUserIsFreelancerOwner()
    {
        await using var context = TestDbContextFactory.Create();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(freelancerUserId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        var proposal = TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId);
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var handler = new UpdateProposalStatusCommandHandler(context, new FixedDateTimeService(DateTime.UtcNow));

        var result = await handler.Handle(
            new UpdateProposalStatusCommand(proposal.ProposalsId, freelancerUserId, new UpdateProposalStatusRequest { Status = 4 }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(4, proposal.Status);
    }
}
