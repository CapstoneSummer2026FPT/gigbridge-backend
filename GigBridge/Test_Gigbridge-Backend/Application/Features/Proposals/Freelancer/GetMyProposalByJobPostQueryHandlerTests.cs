using Application.Common.Exceptions;
using Application.Features.Proposals.Freelancer.GetMyProposalByJobPost.Queries;
using Application.Features.Proposals.Freelancer.GetProposalDetail.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class GetMyProposalByJobPostQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsProposal_WhenProposalExistsForFreelancerAndJobPost()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(userId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        var proposal = TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId);
        context.Users.Add(TestData.User(userId, UserRole.Freelancer, "Freelancer Name"));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();
        var handler = new GetMyProposalByJobPostQueryHandler(context);

        var result = await handler.Handle(new GetMyProposalByJobPostQuery(jobPost.JobPostsId, userId), CancellationToken.None);

        Assert.Equal(proposal.ProposalsId, result.ProposalId);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenProposalDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        context.Users.Add(TestData.User(userId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(TestData.FreelancerProfile(userId));
        await context.SaveChangesAsync();
        var handler = new GetMyProposalByJobPostQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetMyProposalByJobPostQuery(Guid.NewGuid(), userId),
            CancellationToken.None));
    }
}
