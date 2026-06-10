using Application.Common.Exceptions;
using Application.Features.Proposals.Common.GetProposalDetail.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class GetProposalDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsProposal_WhenUserIsClientOwner()
    {
        await using var context = TestDbContextFactory.Create();
        var clientUserId = Guid.NewGuid();
        var clientProfile = TestData.ClientProfile(clientUserId);
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(freelancerUserId);
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);
        var proposal = TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId);
        context.Users.Add(TestData.User(clientUserId, UserRole.Client));
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer, "Freelancer Name"));
        context.ClientProfiles.Add(clientProfile);
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var handler = new GetProposalDetailQueryHandler(context);

        var result = await handler.Handle(new GetProposalDetailQuery(proposal.ProposalsId, clientUserId), CancellationToken.None);

        Assert.Equal(proposal.ProposalsId, result.ProposalId);
    }

    [Fact]
    public async Task Handle_ThrowsUnauthorizedAccessException_WhenUserIsNotOwner()
    {
        await using var context = TestDbContextFactory.Create();
        var clientProfile = TestData.ClientProfile(Guid.NewGuid());
        var freelancerProfile = TestData.FreelancerProfile(Guid.NewGuid());
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);
        var proposal = TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId);
        context.ClientProfiles.Add(clientProfile);
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var handler = new GetProposalDetailQueryHandler(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new GetProposalDetailQuery(proposal.ProposalsId, Guid.NewGuid()),
            CancellationToken.None));
    }
}
