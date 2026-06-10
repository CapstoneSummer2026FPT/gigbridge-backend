using Application.Common.Exceptions;
using Application.Features.JobPosts.Public.GetJobPostDetail.Queries;
using Test_Gigbridge_Backend.Support;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Public;

public class GetJobPostDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsJobPost_WhenJobPostIsOpenAndPublic()
    {
        await using var context = TestDbContextFactory.Create();
        var jobPost = TestData.JobPost(Guid.NewGuid());
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new GetJobPostDetailQueryHandler(context);

        var result = await handler.Handle(new GetJobPostDetailQuery(jobPost.JobPostsId), CancellationToken.None);

        Assert.Equal(jobPost.JobPostsId, result.JobPostsId);
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenJobPostIsPrivate()
    {
        await using var context = TestDbContextFactory.Create();
        var jobPost = TestData.JobPost(Guid.NewGuid(), visibility: 1);
        context.JobPosts.Add(jobPost);
        await context.SaveChangesAsync();
        var handler = new GetJobPostDetailQueryHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetJobPostDetailQuery(jobPost.JobPostsId),
            CancellationToken.None));
    }
}
