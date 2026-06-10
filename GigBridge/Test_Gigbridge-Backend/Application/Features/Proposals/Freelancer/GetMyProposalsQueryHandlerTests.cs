using Application.Common.Exceptions;
using Application.Features.Proposals.Freelancer.GetMyProposals.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Freelancer;

public class GetMyProposalsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsProposals_WhenFreelancerProfileExists()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var profile = TestData.FreelancerProfile(userId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        context.Users.Add(TestData.User(userId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(profile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(TestData.Proposal(jobPost.JobPostsId, profile.FreelancerProfilesId));
        await context.SaveChangesAsync();
        var handler = new GetMyProposalsQueryHandler(context);

        var result = await handler.Handle(new GetMyProposalsQuery { UserId = userId }, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenFreelancerProfileDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetMyProposalsQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetMyProposalsQuery { UserId = Guid.NewGuid() },
            CancellationToken.None));
    }
}
