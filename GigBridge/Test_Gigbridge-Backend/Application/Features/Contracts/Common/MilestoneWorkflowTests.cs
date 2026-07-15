using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Completion.Freelancer.Commands;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;
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
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.ClientUserId,
                    ExternalUrl: "https://example.com/client-wrong-role"),
                CancellationToken.None));

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                "Initial delivery.",
                ExternalUrl: "https://example.com/milestone-1-v1"),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Submitted, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.SubmittedAt);

        await revisionHandler.Handle(
            new RequestMilestoneRevisionCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.Null(fixture.FirstMilestone.ApprovedAt);

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                "Revision delivery.",
                ExternalUrl: "https://example.com/milestone-1-v2"),
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
        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
    }

    [Fact]
    public async Task ListMilestones_DoesNotAutoStartPendingMilestones()
    {
        var fixture = new MilestoneWorkflowFixture();
        var listHandler = new GetContractMilestonesQueryHandler(fixture.Context);

        fixture.ApproveMilestone(fixture.FirstMilestone);

        var milestones = await listHandler.Handle(
            new GetContractMilestonesQuery(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
        Assert.Contains(
            milestones,
            milestone =>
                milestone.MilestoneId == fixture.SecondMilestoneId &&
                milestone.Status == (int)MilestoneStatus.Pending);
    }

    [Fact]
    public async Task StartMilestone_AllowsClientToStartAnyPendingMilestone()
    {
        var fixture = new MilestoneWorkflowFixture();
        var startHandler = new StartMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(7)));

        var response = await startHandler.Handle(
            new StartMilestoneCommand(fixture.ContractId, fixture.ThirdMilestoneId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, response.Status);
        Assert.Equal((int)MilestoneStatus.InProgress, fixture.ThirdMilestone.Status);
        Assert.NotNull(fixture.ThirdMilestone.StartedAt);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.FirstMilestone.Status);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
    }

    [Fact]
    public async Task RequestMilestoneUnlock_NotifiesClientWithoutStartingMilestone()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new RequestMilestoneUnlockCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(8)),
            new NoopNotificationService());

        await handler.Handle(
            new RequestMilestoneUnlockCommand(
                fixture.ContractId,
                fixture.SecondMilestoneId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
        Assert.Contains(
            fixture.Context.Set<Message>().ToList(),
            message => message.Content == "Freelancer requested milestone unlock: Milestone 2.");
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

        Assert.Equal(320m, result.ReleasedAmountVnd);
        Assert.Equal(320m, result.ReleasedTokens);
        Assert.Equal(320m, fixture.FirstMilestone.ReleasedAmount);
        Assert.NotNull(fixture.FirstMilestone.LastReleasedAt);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.FirstMilestone.Status);
        Assert.Equal(320m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.PartiallyReleased, fixture.Escrow.Status);
        Assert.Equal(680m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(320m, fixture.FreelancerWallet.AvailableTokens);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
                CancellationToken.None));

        Assert.Equal(320m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task WithdrawMilestone_DoesNotCompleteContractAfterAllMilestonesReachReleaseCap()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));

        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);

        await handler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);

        await handler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.SecondMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);

        await handler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.ThirdMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Null(fixture.Contract.CompletedAt);
        Assert.Equal(800m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.PartiallyReleased, fixture.Escrow.Status);
        Assert.Equal(200m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(800m, fixture.FreelancerWallet.AvailableTokens);

        var systemMessages = fixture.Context.Set<Message>().ToList();
        Assert.DoesNotContain(
            systemMessages,
            message => message.Content == "Contract completed. Reviews are now open.");
    }

    [Fact]
    public async Task EndProject_OpensClaim_AndFreelancerClaimReleasesRemainingEscrow()
    {
        var fixture = new MilestoneWorkflowFixture();
        var withdrawHandler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));
        var realtime = new CapturingChatRealtimeNotifier();
        var endProjectHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(6)),
            realtime);
        var claimHandler = new ClaimFinalPayoutCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(7)),
            realtime);

        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);

        await withdrawHandler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.FirstMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None);

        var result = await endProjectHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Completed, result.ContractStatus);
        Assert.Equal(0m, result.ReleasedAmountVnd);
        Assert.Equal(320m, result.EscrowReleasedAmountVnd);
        Assert.Equal(fixture.Now.AddMinutes(6), fixture.Contract.CompletedAt);
        Assert.Equal(680m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(320m, fixture.FreelancerWallet.AvailableTokens);

        var claim = await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(680m, claim.ReleasedAmountVnd);
        Assert.Equal(680m, claim.ReleasedTokens);
        Assert.False(claim.AlreadyClaimed);
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(milestone.Amount, milestone.ReleasedAmount));
        Assert.Equal((int)ContractEscrowStatus.Released, fixture.Escrow.Status);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(1_000m, fixture.FreelancerWallet.AvailableTokens);
        Assert.Equal(9, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(4, fixture.EscrowTransactions.Entities.Count);
        Assert.Contains(
            fixture.Context.Set<Message>().ToList(),
            message => message.Content == "Final payout claimed by freelancer.");
        Assert.Contains(realtime.ConversationEvents, evt => evt.EventName == "ContractCompleted");
        Assert.Contains(realtime.UsersEvents, evt => evt.EventName == "ContractCompleted");
        Assert.Contains(realtime.ConversationEvents, evt => evt.EventName == "FinalPayoutClaimed");
        Assert.Contains(realtime.UsersEvents, evt => evt.EventName == "FinalPayoutClaimed");
    }

    [Fact]
    public async Task EndProject_IsIdempotentAfterCompletion()
    {
        var fixture = new MilestoneWorkflowFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            realtime);
        var claimHandler = new ClaimFinalPayoutCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(6)),
            realtime);

        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);

        await handler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        var walletTransactionCount = fixture.WalletTransactions.Entities.Count;
        var escrowTransactionCount = fixture.EscrowTransactions.Entities.Count;

        var retry = await handler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(0m, retry.ReleasedAmountVnd);
        Assert.Equal(walletTransactionCount, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(escrowTransactionCount, fixture.EscrowTransactions.Entities.Count);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(1_000m, fixture.ClientWallet.HeldTokens);
        Assert.DoesNotContain(fixture.Wallets.Entities, wallet => wallet.UserId == fixture.FreelancerUserId);

        await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        var claimRetry = await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.True(claimRetry.AlreadyClaimed);
        Assert.Equal(0m, claimRetry.ReleasedAmountVnd);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(1_000m, fixture.FreelancerWallet.AvailableTokens);
    }

    [Fact]
    public async Task EndProject_RequiresOwningClientAndApprovedMilestones()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            new CapturingChatRealtimeNotifier());

        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ThirdMilestone.Status = (int)MilestoneStatus.Submitted;

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new EndProjectCommand(fixture.ContractId, fixture.FreelancerUserId),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal(1_000m, fixture.ClientWallet.HeldTokens);
    }

    [Fact]
    public async Task ClaimFinalPayout_RequiresSelectedFreelancer()
    {
        var fixture = new MilestoneWorkflowFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var endHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            realtime);
        var claimHandler = new ClaimFinalPayoutCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(6)),
            realtime);
        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);
        await endHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.OutsiderUserId),
            CancellationToken.None));

        Assert.Equal(1_000m, fixture.ClientWallet.HeldTokens);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
    }

    [Fact]
    public async Task ClaimFinalPayout_RollsBackWhenClientHeldBalanceIsInsufficient()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            new CapturingChatRealtimeNotifier());
        var claimHandler = new ClaimFinalPayoutCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(6)),
            new CapturingChatRealtimeNotifier());

        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);
        fixture.ClientWallet.HeldTokens = 999m;

        await handler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        await Assert.ThrowsAsync<BadRequestException>(() => claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None));

        Assert.Equal((int)ContractStatus.Completed, fixture.Contract.Status);
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(0m, milestone.ReleasedAmount));
        Assert.Equal(0m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrow.Status);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);
        Assert.DoesNotContain(fixture.Wallets.Entities, wallet => wallet.UserId == fixture.FreelancerUserId);
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
                TotalBudget = 1_000m,
                Status = (int)ContractStatus.Active,
                CreatedAt = Now
            };
            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000m,
                FundedAmount = 1_000m,
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
                Amount = 400m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            };
            SecondMilestone = new Milestone
            {
                MilestonesId = SecondMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = 300m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            };
            ThirdMilestone = new Milestone
            {
                MilestonesId = ThirdMilestoneId,
                ContractsId = ContractId,
                Title = "Milestone 3",
                Amount = 300m,
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
                AvailableTokens = 10m,
                HeldTokens = 1_000m,
                CreatedAt = Now
            };
            Wallets = Context.AddSet(ClientWallet);
            WalletTransactions = Context.AddSet<WalletTransaction>();
            EscrowTransactions = Context.AddSet<EscrowTransaction>();
            Context.AddSet<Subscription>();
            Context.AddSet<SubscriptionPlan>();
            Context.AddSet(new Conversation
            {
                ConversationsId = Guid.NewGuid(),
                ConversationType = (int)ConversationType.ContractWorkroom,
                Title = "Contract workroom",
                ContractsId = ContractId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            });
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

    private sealed class TestMediaService : IMediaService
    {
        public Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"https://test-storage.com/{folder}/{fileName}");
        }
    }

    [Fact]
    public async Task SubmitMilestone_WithDescriptionAndFile_SavesAttachmentAndDescription()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        var command = new SubmitMilestoneCommand(
            fixture.ContractId,
            fixture.FirstMilestoneId,
            fixture.FreelancerUserId,
            "Completed the deliverable.",
            new SubmitMilestoneFile(new MemoryStream(new byte[] { 1, 2, 3 }), "testfile.pdf", "application/pdf", 3));

        var response = await submitHandler.Handle(command, CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Submitted, response.Status);
        Assert.Equal("Completed the deliverable.", fixture.FirstMilestone.SubmissionDescription);
        Assert.Single(response.Attachments);
        Assert.Equal((int)MilestoneSubmissionSourceType.File, response.Attachments[0].SourceType);
        Assert.Equal("application/pdf", response.Attachments[0].MimeType);

        var attachments = fixture.Context.Set<MilestoneAttachment>()
            .Where(a => a.MilestonesId == fixture.FirstMilestoneId)
            .ToList();

        Assert.Single(attachments);
        Assert.Equal("testfile.pdf", attachments[0].FileName);
        Assert.Equal("https://test-storage.com/milestones/testfile.pdf", attachments[0].FileUrl);
        Assert.Equal(3, attachments[0].FileSize);
        Assert.Equal((int)MilestoneSubmissionSourceType.File, attachments[0].SourceType);
        Assert.Equal("application/pdf", attachments[0].MimeType);
    }

    [Fact]
    public async Task SubmitMilestone_WithLink_SavesLinkAttachmentWithoutUpload()
    {
        var fixture = new MilestoneWorkflowFixture();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        var response = await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                "Published build.",
                ExternalUrl: "https://example.com/build.zip"),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Submitted, response.Status);
        Assert.Equal("Published build.", response.SubmissionDescription);
        Assert.Single(response.Attachments);
        Assert.Equal((int)MilestoneSubmissionSourceType.Link, response.Attachments[0].SourceType);
        Assert.Equal("https://example.com/build.zip", response.Attachments[0].FileUrl);
        Assert.Equal("External URL", response.Attachments[0].FileName);
        Assert.Null(response.Attachments[0].FileSize);
        Assert.Null(response.Attachments[0].MimeType);
    }

    [Fact]
    public async Task SubmitMilestone_DoesNotAutoStartPendingMilestone()
    {
        var fixture = new MilestoneWorkflowFixture();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(4)));

        fixture.ApproveMilestone(fixture.FirstMilestone);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.SecondMilestoneId,
                fixture.FreelancerUserId,
                "Milestone 2 delivery.",
                ExternalUrl: "https://example.com/milestone-2"),
            CancellationToken.None));

        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
        Assert.Null(fixture.SecondMilestone.StartedAt);
        Assert.Null(fixture.SecondMilestone.SubmittedAt);
    }

    [Fact]
    public async Task SubmitMilestone_RequiresExactlyOneValidSource()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;
        var validFile = new SubmitMilestoneFile(
            new MemoryStream(new byte[] { 1 }),
            "deliverable.pdf",
            "application/pdf",
            1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId,
                    File: validFile,
                    ExternalUrl: "https://example.com/build.zip"),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId,
                    ExternalUrl: "ftp://example.com/build.zip"),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId,
                    File: new SubmitMilestoneFile(
                        new MemoryStream(new byte[] { 1 }),
                        "huge.zip",
                        "application/zip",
                        100 * 1024 * 1024 + 1)),
                CancellationToken.None));
    }
}
