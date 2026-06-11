using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class MilestoneWorkflowTests
{
    [Fact]
    public async Task MilestoneLifecycle_EnforcesParticipantRolesAndTransitions()
    {
        var fixture = new MilestoneWorkflowFixture();
        var startHandler = new StartMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)));
        var approveHandler = new ApproveMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(2)));
        var revisionHandler = new RequestMilestoneRevisionCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(3)));
        var listHandler = new GetContractMilestonesQueryHandler(fixture.Context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            listHandler.Handle(
                new GetContractMilestonesQuery(fixture.ContractId, fixture.OutsiderUserId),
                CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            startHandler.Handle(
                new StartMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
                CancellationToken.None));

        var milestones = await listHandler.Handle(
            new GetContractMilestonesQuery(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal(3, milestones.Count);

        await startHandler.Handle(
            new StartMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.StartedAt);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
                CancellationToken.None));

        await submitHandler.Handle(
            new SubmitMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Submitted, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.SubmittedAt);

        await revisionHandler.Handle(
            new RequestMilestoneRevisionCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.Null(fixture.FirstMilestone.ApprovedAt);

        await submitHandler.Handle(
            new SubmitMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            approveHandler.Handle(
                new ApproveMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
                CancellationToken.None));

        await approveHandler.Handle(
            new ApproveMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Approved, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.ApprovedAt);
    }

    [Fact]
    public async Task WithdrawMilestone_ReleasesEightyPercentAfterHalfMilestonesApproved()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));

        fixture.ApproveMilestone(fixture.FirstMilestone);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
                CancellationToken.None));

        fixture.ApproveMilestone(fixture.SecondMilestone);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
                CancellationToken.None));

        var result = await handler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(320_000m, result.ReleasedAmountVnd);
        Assert.Equal(320m, result.ReleasedTokens);
        Assert.Equal(320_000m, fixture.FirstMilestone.ReleasedAmount);
        Assert.NotNull(fixture.FirstMilestone.LastReleasedAt);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.FirstMilestone.Status);
        Assert.Equal(320_000m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.PartiallyReleased, fixture.Escrow.Status);
        Assert.Equal(680m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(320m, fixture.FreelancerWallet.AvailableTokens);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
                CancellationToken.None));

        Assert.Equal(320_000m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    private sealed class MilestoneWorkflowFixture
    {
        public MilestoneWorkflowFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = Guid.NewGuid(),
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Active contract",
                TotalBudget = 1_000_000m,
                Status = (int)ContractStatus.Active,
                CreatedAt = Now
            };
            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000_000m,
                FundedAmount = 1_000_000m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.Funded,
                CreatedAt = Now,
                FundedAt = Now
            };
            FirstMilestone = new Milestone
            {
                MilestonesId = FirstMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 400_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            };
            SecondMilestone = new Milestone
            {
                MilestonesId = SecondMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = 300_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            };
            ThirdMilestone = new Milestone
            {
                MilestonesId = ThirdMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 3",
                Amount = 300_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 2,
                CreatedAt = Now
            };

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" },
                new User { UserId = OutsiderUserId, Role = (int)UserRole.Client, Email = "outsider@example.com", FullName = "Outsider" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Milestones = Context.AddSet(FirstMilestone, SecondMilestone, ThirdMilestone);
            Escrows = Context.AddSet(Escrow);
            ClientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = ClientUserId,
                AvailableTokens = 0m,
                HeldTokens = 1_000m,
                CreatedAt = Now
            };
            Wallets = Context.AddSet(ClientWallet);
            WalletTransactions = Context.AddSet<WalletTransaction>();
            EscrowTransactions = Context.AddSet<EscrowTransaction>();
            Context.AddSet<Subscription>();
            Context.AddSet<SubscriptionPlan>();
            Context.AddSet<Conversation>();
            Context.AddSet<Message>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid OutsiderUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid FirstMilestoneId { get; } = Guid.NewGuid();
        public Guid SecondMilestoneId { get; } = Guid.NewGuid();
        public Guid ThirdMilestoneId { get; } = Guid.NewGuid();
        public TestDbSet<Milestone> Milestones { get; }
        public TestDbSet<ContractEscrow> Escrows { get; }
        public TestDbSet<UserWallet> Wallets { get; }
        public TestDbSet<WalletTransaction> WalletTransactions { get; }
        public TestDbSet<EscrowTransaction> EscrowTransactions { get; }
        public UserWallet ClientWallet { get; }
        public Contract Contract { get; }
        public ContractEscrow Escrow { get; }
        public Milestone FirstMilestone { get; }
        public Milestone SecondMilestone { get; }
        public Milestone ThirdMilestone { get; }

        public UserWallet FreelancerWallet =>
            Wallets.Entities.Single(wallet => wallet.UserId == FreelancerUserId);

        public void ApproveMilestone(Milestone milestone)
        {
            milestone.Status = (int)MilestoneStatus.Approved;
            milestone.SubmittedAt = Now;
            milestone.ApprovedAt = Now;
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
