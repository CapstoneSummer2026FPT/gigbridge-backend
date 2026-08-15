using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using Domain.Entities;
using Application.Common.InternalServices.JobPosts.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateStatusJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_OpenStatusWithValidProjectRequest_UpdatesJobPostStatus()
    {
        var fixture = new UpdateStatusFixture();

        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            new UpdateStatusJobPostCommand(
                fixture.JobPostId,
                fixture.ClientUserId,
                new UpdateStatusJobPostRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, fixture.JobPost.Status);
        Assert.Equal(fixture.Now, fixture.JobPost.UpdatedAt);
    }

    [Fact]
    public async Task Handle_OpenStatusWithoutCategory_ThrowsBadRequest()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.MajorCategoryId = null;

        var handler = fixture.CreateHandler();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 1 }),
                CancellationToken.None));

        Assert.Equal("Project request category is required before publishing.", exception.Message);
        Assert.Equal(0, fixture.JobPost.Status);
    }

    [Fact]
    public async Task Handle_OpenStatusWithoutRequirementDetails_ThrowsBadRequest()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.Description = " ";

        var handler = fixture.CreateHandler();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 1 }),
                CancellationToken.None));

        Assert.Equal("Project requirement details are required before publishing.", exception.Message);
        Assert.Equal(0, fixture.JobPost.Status);
    }

    [Fact]
    public async Task Handle_OpenStatusWithMilestoneWithoutDeadline_ThrowsBadRequest()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.JobPostMilestonePlans.Add(new JobPostMilestonePlan
        {
            Title = "Delivery",
            Amount = 500m,
            EstimatedDuration = "1 week",
            Deliverables = "Working release",
            AcceptanceCriteria = "Acceptance tests pass",
            OrderIndex = 0
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            fixture.CreateHandler().Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 1 }),
                CancellationToken.None));

        Assert.Contains("deadline", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.JobPost.Status);
    }

    [Fact]
    public async Task Handle_OpenStatusWithValidYearDurationAndDeadline_UpdatesJobPostStatus()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.EndDate = fixture.Now.AddDays(7);
        fixture.JobPost.JobPostMilestonePlans.Add(new JobPostMilestonePlan
        {
            Title = "Delivery",
            Amount = 500m,
            EstimatedDuration = "1 year",
            DueDate = DateOnly.FromDateTime(fixture.Now.AddYears(1)),
            Deliverables = "Working release",
            AcceptanceCriteria = "Acceptance tests pass",
            OrderIndex = 0
        });

        var result = await fixture.CreateHandler().Handle(
            new UpdateStatusJobPostCommand(
                fixture.JobPostId,
                fixture.ClientUserId,
                new UpdateStatusJobPostRequest { Status = 1 }),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, fixture.JobPost.Status);
    }

    [Fact]
    public async Task Handle_OpenStatusWithIllegalContent_ThrowsValidationException()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.Description = "Cho thue tai khoan ngan hang va nhan tien ho.";

        var handler = fixture.CreateHandler();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 1 }),
                CancellationToken.None));

        Assert.Contains(
            "Job post appears to contain money laundering or suspicious payment transfer activity.",
            exception.Errors["JobPostContent"]);
        Assert.Equal(0, fixture.JobPost.Status);
    }

    [Fact]
    public async Task Handle_NonOpenStatusWithIllegalContent_ThrowsValidationException()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.Description = "Hack tai khoan nguoi dung.";

        var handler = fixture.CreateHandler();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 2 }),
                CancellationToken.None));

        Assert.Contains(
            "Job post appears to contain cybercrime, malware, hacking, or credential theft-related work.",
            exception.Errors["JobPostContent"]);
        Assert.Equal(0, fixture.JobPost.Status);
    }

    private sealed class UpdateStatusFixture
    {
        public UpdateStatusFixture()
        {
            Context.AddSet(new ClientProfile
            {
                ClientProfilesId = ClientProfileId,
                UserId = ClientUserId
            });

            JobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Draft setup",
                Description = "Complete setup",
                MajorCategoryId = MajorCategoryId,
                Status = 0,
                CreatedAt = Now
            };

            Context.AddSet(JobPost);
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 25, 8, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid MajorCategoryId { get; } = Guid.NewGuid();
        public JobPost JobPost { get; }

        public UpdateStatusJobPostCommandHandler CreateHandler()
        {
            return new UpdateStatusJobPostCommandHandler(
                Context,
                new FixedDateTimeService(Now),
                new ContentModerationService());
        }

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
