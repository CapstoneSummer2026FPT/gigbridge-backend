using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Contracts.JobPostSetup.Complete.Commands;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.ESign;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class CompleteJobPostContractSetupCommandHandlerTests
{
    [Fact]
    public async Task Handle_DraftJobPostWithValidSetup_PublishesJobPost()
    {
        var fixture = new JobPostSetupFixture();
        fixture.AddFullySignedDocument();
        fixture.AddValidMilestone();

        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, fixture.JobPost.Status);
        Assert.Equal(fixture.Now, fixture.JobPost.UpdatedAt);
        Assert.Equal(1, fixture.Context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_OpenJobPostWithValidSetup_ReturnsSuccessWithoutRepublishing()
    {
        var fixture = new JobPostSetupFixture(jobPostStatus: 1);
        fixture.AddFullySignedDocument();
        fixture.AddValidMilestone();

        var handler = fixture.CreateHandler();

        var result = await handler.Handle(
            new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1, fixture.JobPost.Status);
        Assert.Null(fixture.JobPost.UpdatedAt);
        Assert.Equal(0, fixture.Context.SaveChangesCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Handle_ClosedOrCancelledJobPost_ThrowsBadRequest(int jobPostStatus)
    {
        var fixture = new JobPostSetupFixture(jobPostStatus);
        fixture.AddFullySignedDocument();
        fixture.AddValidMilestone();

        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingFullySignedDocument_ThrowsBadRequest()
    {
        var fixture = new JobPostSetupFixture();
        fixture.AddValidMilestone();

        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Milestone 1", 0)]
    public async Task Handle_InvalidMilestone_ThrowsBadRequest(string title, decimal amount)
    {
        var fixture = new JobPostSetupFixture();
        fixture.AddFullySignedDocument();
        fixture.AddMilestone(title, amount);

        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MilestoneTotalExceedsContractBudget_ThrowsBadRequest()
    {
        var fixture = new JobPostSetupFixture();
        fixture.AddFullySignedDocument();
        fixture.AddMilestone("Milestone 1", 101m);

        var handler = fixture.CreateHandler();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CompleteJobPostContractSetupCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        Assert.Contains("cannot exceed contract total budget", exception.Message);
    }

    private sealed class JobPostSetupFixture
    {
        public JobPostSetupFixture(int jobPostStatus = 0)
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
                Status = jobPostStatus,
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

        public CompleteJobPostContractSetupCommandHandler CreateHandler()
        {
            return new CompleteJobPostContractSetupCommandHandler(
                Context,
                new FixedDateTimeService(Now));
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
                Status = (int)ESignDocumentStatus.FullySigned,
                CreatedAt = Now
            });
        }

        public void AddValidMilestone()
        {
            AddMilestone("Milestone 1", 100m);
        }

        public void AddMilestone(string title, decimal amount)
        {
            var milestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = title,
                Amount = amount,
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
