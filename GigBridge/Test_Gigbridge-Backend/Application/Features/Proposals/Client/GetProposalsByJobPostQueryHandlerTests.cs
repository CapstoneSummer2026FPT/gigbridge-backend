using Application.Common.Exceptions;
using Application.Features.Proposals.Client.GetProposalsByJobPost.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Client;

public class GetProposalsByJobPostQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsProposals_WhenClientOwnsJobPost()
    {
        await using var context = TestDbContextFactory.Create();
        var clientUserId = Guid.NewGuid();
        var clientProfile = TestData.ClientProfile(clientUserId);
        var freelancerProfile = TestData.FreelancerProfile(Guid.NewGuid());
        var jobPost = TestData.JobPost(clientProfile.ClientProfilesId);
        context.Users.Add(TestData.User(clientUserId, UserRole.Client));
        context.ClientProfiles.Add(clientProfile);
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId));
        await context.SaveChangesAsync();
        var handler = new GetProposalsByJobPostQueryHandler(context);

        var result = await handler.Handle(
            new GetProposalsByJobPostQuery { UserId = clientUserId, JobPostsId = jobPost.JobPostsId },
            CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_ThrowsForbiddenAccessException_WhenClientDoesNotOwnJobPost()
    {
        await using var context = TestDbContextFactory.Create();
        var ownerProfile = TestData.ClientProfile(Guid.NewGuid());
        var otherUserId = Guid.NewGuid();
        context.ClientProfiles.Add(ownerProfile);
        context.ClientProfiles.Add(TestData.ClientProfile(otherUserId));
        var jobPost = TestData.JobPost(ownerProfile.ClientProfilesId);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new GetProposalsByJobPostQueryHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new GetProposalsByJobPostQuery { UserId = otherUserId, JobPostsId = jobPost.JobPostsId },
            CancellationToken.None));
    }
}
