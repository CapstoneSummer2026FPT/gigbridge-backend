using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;
using Application.Features.JobPosts.Common;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateVisibilityJobPostCommandHandlerTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(null, 1)]
    public async Task Handle_PublishedPublicOrInviteOnlyToPrivate_ThrowsBadRequest(
        int? currentVisibility,
        int requestedVisibility)
    {
        var fixture = new UpdateVisibilityFixture(status: 1, visibility: currentVisibility);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.CreateHandler().Handle(
                fixture.CreateCommand(requestedVisibility),
                CancellationToken.None));

        Assert.Equal(JobPostEditingGuard.PrivateTransitionLockedMessage, exception.Message);
        Assert.Equal(currentVisibility, fixture.JobPost.Visibility);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public async Task Handle_PublishedPublicAndInviteOnly_CanSwitchBetweenScopes(
        int currentVisibility,
        int requestedVisibility)
    {
        var fixture = new UpdateVisibilityFixture(status: 1, visibility: currentVisibility);

        var result = await fixture.CreateHandler().Handle(
            fixture.CreateCommand(requestedVisibility),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(requestedVisibility, fixture.JobPost.Visibility);
        Assert.Equal(fixture.Now, fixture.JobPost.UpdatedAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public async Task Handle_DraftJob_AllowsAnySupportedScope(
        int currentVisibility,
        int requestedVisibility)
    {
        var fixture = new UpdateVisibilityFixture(status: 0, visibility: currentVisibility);

        var result = await fixture.CreateHandler().Handle(
            fixture.CreateCommand(requestedVisibility),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(requestedVisibility, fixture.JobPost.Visibility);
    }

    [Fact]
    public async Task Handle_AdminLockedJob_ThrowsExistingLockError()
    {
        var fixture = new UpdateVisibilityFixture(
            status: 1,
            visibility: JobPostEditingGuard.AdminLockedVisibility);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.CreateHandler().Handle(
                fixture.CreateCommand(JobPostEditingGuard.PublicVisibility),
                CancellationToken.None));

        Assert.Equal(JobPostEditingGuard.AdminLockedMessage, exception.Message);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    private sealed class UpdateVisibilityFixture
    {
        private readonly Guid _userId = Guid.NewGuid();

        public UpdateVisibilityFixture(int status, int? visibility)
        {
            var clientProfileId = Guid.NewGuid();
            Context.AddSet(new ClientProfile
            {
                ClientProfilesId = clientProfileId,
                UserId = _userId
            });

            JobPost = new JobPost
            {
                JobPostsId = Guid.NewGuid(),
                ClientProfilesId = clientProfileId,
                Title = "Visibility workflow",
                Description = "Visibility workflow details",
                Status = status,
                Visibility = visibility,
                CreatedAt = Now.AddDays(-1)
            };
            Context.AddSet(JobPost);
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        public JobPost JobPost { get; }

        public UpdateVisibilityJobPostCommandHandler CreateHandler() => new(
            Context,
            new FixedDateTimeService(Now));

        public UpdateVisibilityJobPostCommand CreateCommand(int visibility) => new(
            JobPost.JobPostsId,
            _userId,
            new UpdateVisibilityJobPostRequest { Visibility = visibility });
    }

    private sealed class FixedDateTimeService(DateTime utcNow) : IDateTimeService
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
