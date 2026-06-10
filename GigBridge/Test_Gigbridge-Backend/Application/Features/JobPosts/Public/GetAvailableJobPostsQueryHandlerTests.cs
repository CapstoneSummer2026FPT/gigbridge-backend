using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Public;

public class GetAvailableJobPostsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyOpenPublicJobPosts()
    {
        await using var context = TestDbContextFactory.Create();
        var clientProfileId = Guid.NewGuid();
        context.JobPosts.Add(TestData.JobPost(clientProfileId, title: "Visible job", status: 1, visibility: 0));
        context.JobPosts.Add(TestData.JobPost(clientProfileId, title: "Closed job", status: 2, visibility: 0));
        context.JobPosts.Add(TestData.JobPost(clientProfileId, title: "Private job", status: 1, visibility: 1));
        await context.SaveChangesAsync();
        var handler = new GetAvailableJobPostsQueryHandler(context);

        var result = await handler.Handle(new GetAvailableJobPostsQuery(), CancellationToken.None);

        var jobPost = Assert.Single(result);
        Assert.Equal("Visible job", jobPost.Title);
    }

    [Fact]
    public async Task Handle_ReturnsFilteredJobPosts_WhenSearchMatchesSkill()
    {
        await using var context = TestDbContextFactory.Create();
        var clientProfileId = Guid.NewGuid();
        var skill = TestData.Skill(name: "React");
        var matchingJob = TestData.JobPost(clientProfileId, title: "Frontend job");
        var otherJob = TestData.JobPost(clientProfileId, title: "Backend job");
        context.AddRange(skill, matchingJob, otherJob);
        context.JobPostSkills.Add(TestData.JobPostSkill(matchingJob.JobPostsId, skill.SkillsId));
        await context.SaveChangesAsync();
        var handler = new GetAvailableJobPostsQueryHandler(context);

        var result = await handler.Handle(new GetAvailableJobPostsQuery(Search: "react"), CancellationToken.None);

        var jobPost = Assert.Single(result);
        Assert.Equal(matchingJob.JobPostsId, jobPost.JobPostsId);
    }
}
