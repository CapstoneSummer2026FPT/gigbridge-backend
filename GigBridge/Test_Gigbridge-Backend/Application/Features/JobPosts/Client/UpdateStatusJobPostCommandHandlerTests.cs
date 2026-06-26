using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.JobPosts.Client;

public class UpdateStatusJobPostCommandHandlerTests
{
    [Fact]
    public async Task Handle_OpenStatusWithValidSetup_UpdatesJobPostStatus()
    {
        var fixture = new UpdateStatusFixture();
        fixture.AddFullySignedDocument();
        fixture.AddValidMilestone();

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
    public async Task Handle_OpenStatusWithoutFullySignedDocument_ThrowsBadRequest()
    {
        var fixture = new UpdateStatusFixture();
        fixture.AddValidMilestone();

        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateStatusJobPostCommand(
                    fixture.JobPostId,
                    fixture.ClientUserId,
                    new UpdateStatusJobPostRequest { Status = 1 }),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_OpenStatusWithIllegalContent_ThrowsValidationException()
    {
        var fixture = new UpdateStatusFixture();
        fixture.JobPost.Description = "Cho thue tai khoan ngan hang va nhan tien ho.";
        fixture.AddFullySignedDocument();
        fixture.AddValidMilestone();

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
                Status = 0,
                CreatedAt = Now
            };

            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Draft setup",
                TotalBudget = 100m,
                Status = (int)ContractStatus.PendingFreelancerSelection,
                CreatedAt = Now
            };

            Context.AddSet(JobPost);
            Context.AddSet(Contract);
            Milestones = Context.AddSet<Milestone>();
            Context.AddSet<EsignDocument>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 25, 8, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public JobPost JobPost { get; }
        public Contract Contract { get; }
        public TestDbSet<Milestone> Milestones { get; }

        public UpdateStatusJobPostCommandHandler CreateHandler()
        {
            return new UpdateStatusJobPostCommandHandler(
                Context,
                new FixedDateTimeService(Now),
                new ContentModerationService());
        }

        public void AddFullySignedDocument()
        {
            Context.Set<EsignDocument>().Add(new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                EsignTemplatesId = Guid.NewGuid(),
                JobPostsId = JobPostId,
                ContractsId = ContractId,
                DocumentCode = "GB-TEST",
                RenderedHtmlContent = "<html>job post</html>",
                Status = (int)ESignDocumentStatus.FullySigned,
                CreatedAt = Now
            });
        }

        public void AddValidMilestone()
        {
            var milestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 100m,
                Status = (int)MilestoneStatus.Pending,
                CreatedAt = Now
            };

            Contract.Milestones.Add(milestone);
            Milestones.Add(milestone);
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
