using Application.Common.Exceptions;
using Application.Features.JobPosts.Freelancer.GetMyAppliedJobPosts.Queries;
using Domain.Enums;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Freelancer;

public class GetMyAppliedJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAppliedJobPosts_WhenFreelancerProfileExists()
    {
        await using var context = TestDbContextFactory.Create();
        var freelancerUserId = Guid.NewGuid();
        var freelancerProfile = TestData.FreelancerProfile(freelancerUserId);
        var jobPost = TestData.JobPost(Guid.NewGuid());
        context.Users.Add(TestData.User(freelancerUserId, UserRole.Freelancer));
        context.FreelancerProfiles.Add(freelancerProfile);
        context.JobPosts.Add(jobPost);
        context.Proposals.Add(TestData.Proposal(jobPost.JobPostsId, freelancerProfile.FreelancerProfilesId));
        await context.SaveChangesAsync();
        var handler = new GetMyAppliedJobPostsQueryHandler(context);

        var result = await handler.Handle(new GetMyAppliedJobPostsQuery { UserId = freelancerUserId }, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenFreelancerProfileDoesNotExist()
    {
        await using var context = TestDbContextFactory.Create();
        var handler = new GetMyAppliedJobPostsQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetMyAppliedJobPostsQuery { UserId = Guid.NewGuid() },
            CancellationToken.None));
    }
}
