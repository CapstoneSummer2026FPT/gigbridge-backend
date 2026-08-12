using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class CreateDraftJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesDraftJobPostWithoutContractOrSkills()
    {
        var now = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();

        context.AddSet(new ClientProfile { ClientProfilesId = clientProfileId, UserId = userId });
        var jobPosts = context.AddSet<JobPost>();
        var contracts = context.AddSet<Contract>();
        var jobPostSkills = context.AddSet<JobPostSkill>();

        var handler = new CreateDraftJobPostCommandHandler(context, new FixedDateTimeService(now));

        var result = await handler.Handle(new CreateDraftJobPostCommand(userId), CancellationToken.None);

        var jobPost = Assert.Single(jobPosts.Entities);
        Assert.Equal(jobPost.JobPostsId, result.JobPostId);
        Assert.Equal(0, result.Status);
        Assert.Equal(clientProfileId, jobPost.ClientProfilesId);
        Assert.Equal("Untitled Job Post", jobPost.Title);
        Assert.Equal(string.Empty, jobPost.Description);
        Assert.Equal(0, jobPost.Status);
        Assert.Equal(0, jobPost.Visibility);
        Assert.Equal(now, jobPost.CreatedAt);
        Assert.Empty(contracts.Entities);
        Assert.Empty(jobPostSkills.Entities);
    }

    [Fact]
    public async Task Handle_ThrowsWhenClientProfileDoesNotExist()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<ClientProfile>();
        context.AddSet<JobPost>();

        var handler = new CreateDraftJobPostCommandHandler(
            context,
            new FixedDateTimeService(DateTime.UtcNow));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateDraftJobPostCommand(Guid.NewGuid()), CancellationToken.None));
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
