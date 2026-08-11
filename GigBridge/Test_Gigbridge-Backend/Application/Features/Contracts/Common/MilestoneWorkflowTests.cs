using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Completion.Freelancer.Commands;
using Application.Features.Contracts.Milestones.Client.Approve.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.Commands;
using Application.Features.Contracts.Milestones.Client.RequestRevision.DTOs;
using Application.Features.Contracts.Milestones.Client.Start.Commands;
using Application.Features.Contracts.Milestones.Common.Get.Queries;
using Application.Features.Contracts.Milestones.Common.List.Queries;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Domain.Entities;
using Domain.Enums;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class MilestoneWorkflowTests
{
    private static readonly byte[] ValidZipContent = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] ValidPdfContent = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A];

    private static SubmitMilestoneFile CreateSubmissionFile(string fileName) =>
        new(
            new MemoryStream(ValidZipContent),
            fileName,
            "application/zip",
            ValidZipContent.Length);

    [Fact]
    public async Task MilestoneLifecycle_EnforcesParticipantRolesAndTransitions()
    {
        var fixture = new MilestoneWorkflowFixture();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)),
            new TestMediaService());
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
        var milestones = await listHandler.Handle(
            new GetContractMilestonesQuery(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal(3, milestones.Count);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;
        fixture.FirstMilestone.StartedAt = fixture.Now;
        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.StartedAt);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            submitHandler.Handle(
                new SubmitMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.ClientUserId,
                    File: CreateSubmissionFile("client-wrong-role.zip")),
                CancellationToken.None));

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                "Initial delivery.",
                File: CreateSubmissionFile("milestone-1-v1.zip")),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Submitted, fixture.FirstMilestone.Status);
        Assert.NotNull(fixture.FirstMilestone.SubmittedAt);

        await revisionHandler.Handle(
            new RequestMilestoneRevisionCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.ClientUserId,
                new RequestMilestoneRevisionRequest("Authentication flow needs revision.", [fixture.FirstWorkItem.ContractWorkItemId])),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.Null(fixture.FirstMilestone.ApprovedAt);
        fixture.FirstWorkItem.Status = (int)ContractWorkItemStatus.Completed;

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                "Revision delivery.",
                File: CreateSubmissionFile("milestone-1-v2.zip")),
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
        Assert.Equal(0m, fixture.FirstMilestone.ReleasedAmount);
        Assert.Equal(0m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(1_000_000m, fixture.ClientWallet.HeldTokens);
        Assert.DoesNotContain(fixture.Wallets.Entities, wallet => wallet.UserId == fixture.FreelancerUserId);
        Assert.Empty(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);
        Assert.Equal((int)MilestoneStatus.InProgress, fixture.SecondMilestone.Status);
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
    public async Task StartMilestone_RejectsDeprecatedManualStart()
    {
        var fixture = new MilestoneWorkflowFixture();
        var startHandler = new StartMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(7)));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => startHandler.Handle(
            new StartMilestoneCommand(fixture.ContractId, fixture.ThirdMilestoneId, fixture.ClientUserId),
            CancellationToken.None));

        Assert.Contains("deprecated", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.ThirdMilestone.Status);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.FirstMilestone.Status);
        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
    }

    [Fact]
    public async Task RequestMilestoneUnlock_PersistsEarlyStartRequestWithoutStartingMilestone()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new RequestMilestoneUnlockCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(8)),
            new NoopNotificationService());
        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;
        fixture.FirstMilestone.StartedAt = fixture.Now;

        await handler.Handle(
            new RequestMilestoneUnlockCommand(
                fixture.ContractId,
                fixture.SecondMilestoneId,
                fixture.FreelancerUserId,
                "Begin integration while milestone one is in review."),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
        var request = Assert.Single(fixture.Context.Set<MilestoneEarlyStartRequest>());
        Assert.Equal(fixture.SecondMilestoneId, request.MilestonesId);
        Assert.Equal("Begin integration while milestone one is in review.", request.Reason);
        Assert.Equal((int)MilestoneEarlyStartRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task GetMilestoneById_RejectsMismatchedContractRoute()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new GetMilestoneByIdQueryHandler(fixture.Context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetMilestoneByIdQuery(
                    fixture.FirstMilestoneId,
                    fixture.ClientUserId,
                    Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApprovingAllMilestones_DoesNotReleaseEscrow()
    {
        var fixture = new MilestoneWorkflowFixture();
        await fixture.ApproveThroughWorkflowAsync(
            fixture.FirstMilestone,
            fixture.SecondMilestone,
            fixture.ThirdMilestone);

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Null(fixture.Contract.CompletedAt);
        Assert.Equal(0m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrow.Status);
        Assert.Equal(1_000_000m, fixture.ClientWallet.HeldTokens);
        Assert.DoesNotContain(fixture.Wallets.Entities, wallet => wallet.UserId == fixture.FreelancerUserId);
        Assert.Empty(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);

        var systemMessages = fixture.Context.Set<Message>().ToList();
        Assert.DoesNotContain(
            systemMessages,
            message => message.Content == "Contract completed. Reviews are now open.");
    }

    [Fact]
    public async Task WithdrawMilestone_ReleasesEightyPercentAfterHalfMilestonesApproved()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));

        fixture.ApproveMilestone(fixture.FirstMilestone);

        var thresholdException = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId),
                CancellationToken.None));
        Assert.Contains("50%", thresholdException.Message, StringComparison.OrdinalIgnoreCase);

        fixture.ApproveMilestone(fixture.SecondMilestone);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.ClientUserId),
                CancellationToken.None));

        var result = await handler.Handle(
            new WithdrawMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(320_000m, result.ReleasedAmountVnd);
        Assert.Equal(320_000m, result.ReleasedTokens);
        Assert.Equal(320_000m, fixture.FirstMilestone.ReleasedAmount);
        Assert.NotNull(fixture.FirstMilestone.LastReleasedAt);
        Assert.Equal(320_000m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.PartiallyReleased, fixture.Escrow.Status);
        Assert.Equal(680_000m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(0m, fixture.FreelancerWallet.AvailableTokens);
        Assert.Equal(320_000m, fixture.FreelancerWallet.WithdrawableTokens);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);
        Assert.Equal(2, fixture.Context.TransactionBeginCount);
        Assert.Equal(2, fixture.Context.TransactionLockCount);
        Assert.Equal(1, fixture.Context.TransactionCommitCount);

        Assert.Empty(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Contains(
            fixture.Context.Set<Message>().ToList(),
            message => message.Content == "Milestone early withdrawal released: Milestone 1.");

        var walletTransactionCount = fixture.WalletTransactions.Entities.Count;
        var escrowTransactionCount = fixture.EscrowTransactions.Entities.Count;
        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId),
                CancellationToken.None));

        Assert.Equal(walletTransactionCount, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(escrowTransactionCount, fixture.EscrowTransactions.Entities.Count);
        Assert.Equal(320_000m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(3, fixture.Context.TransactionBeginCount);
        Assert.Equal(3, fixture.Context.TransactionLockCount);
        Assert.Equal(1, fixture.Context.TransactionCommitCount);
    }

    [Fact]
    public async Task WithdrawMilestone_RejectsUnapprovedMilestoneInvalidEscrowAndInsufficientHeldBalance()
    {
        var unapprovedFixture = new MilestoneWorkflowFixture();
        var unapprovedHandler = new WithdrawMilestoneCommandHandler(
            unapprovedFixture.Context,
            new FixedDateTimeService(unapprovedFixture.Now));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            unapprovedHandler.Handle(
                new WithdrawMilestoneCommand(
                    unapprovedFixture.ContractId,
                    unapprovedFixture.FirstMilestoneId,
                    unapprovedFixture.FreelancerUserId),
                CancellationToken.None));

        var invalidEscrowFixture = new MilestoneWorkflowFixture();
        invalidEscrowFixture.ApproveMilestone(invalidEscrowFixture.FirstMilestone);
        invalidEscrowFixture.ApproveMilestone(invalidEscrowFixture.SecondMilestone);
        invalidEscrowFixture.Escrow.Status = (int)ContractEscrowStatus.PendingFunding;
        var invalidEscrowHandler = new WithdrawMilestoneCommandHandler(
            invalidEscrowFixture.Context,
            new FixedDateTimeService(invalidEscrowFixture.Now));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            invalidEscrowHandler.Handle(
                new WithdrawMilestoneCommand(
                    invalidEscrowFixture.ContractId,
                    invalidEscrowFixture.FirstMilestoneId,
                    invalidEscrowFixture.FreelancerUserId),
                CancellationToken.None));

        var insufficientFixture = new MilestoneWorkflowFixture();
        insufficientFixture.ApproveMilestone(insufficientFixture.FirstMilestone);
        insufficientFixture.ApproveMilestone(insufficientFixture.SecondMilestone);
        insufficientFixture.ClientWallet.HeldTokens = 0m;
        var insufficientHandler = new WithdrawMilestoneCommandHandler(
            insufficientFixture.Context,
            new FixedDateTimeService(insufficientFixture.Now));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            insufficientHandler.Handle(
                new WithdrawMilestoneCommand(
                    insufficientFixture.ContractId,
                    insufficientFixture.FirstMilestoneId,
                    insufficientFixture.FreelancerUserId),
                CancellationToken.None));

        Assert.Empty(insufficientFixture.WalletTransactions.Entities);
        Assert.Empty(insufficientFixture.EscrowTransactions.Entities);
        Assert.DoesNotContain(
            insufficientFixture.Wallets.Entities,
            wallet => wallet.UserId == insufficientFixture.FreelancerUserId);
    }

    [Fact]
    public async Task WithdrawMilestone_TreatsLegacyAutomaticReleaseAsAlreadyWithdrawn()
    {
        var fixture = new MilestoneWorkflowFixture();
        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.FirstMilestone.ReleasedAmount = 320_000m;
        fixture.FirstMilestone.LastReleasedAt = fixture.Now;
        fixture.Escrow.ReleasedAmount = 320_000m;
        fixture.Escrow.Status = (int)ContractEscrowStatus.PartiallyReleased;
        fixture.ClientWallet.HeldTokens = 680_000m;
        var handler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new WithdrawMilestoneCommand(
                    fixture.ContractId,
                    fixture.FirstMilestoneId,
                    fixture.FreelancerUserId),
                CancellationToken.None));

        Assert.Equal(320_000m, fixture.FirstMilestone.ReleasedAmount);
        Assert.Equal(320_000m, fixture.Escrow.ReleasedAmount);
        Assert.Empty(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);
        Assert.DoesNotContain(fixture.Wallets.Entities, wallet => wallet.UserId == fixture.FreelancerUserId);
    }

    [Fact]
    public async Task EndProject_ReleasesFinalTwentyPercent_AndLegacyClaimIsIdempotent()
    {
        var fixture = new MilestoneWorkflowFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var notifications = Substitute.For<INotificationService>();
        var endProjectHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(6)),
            realtime,
            notifications);
        var claimHandler = new ClaimFinalPayoutCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(7)),
            realtime);
        var withdrawHandler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)));

        await fixture.ApproveThroughWorkflowAsync(
            fixture.FirstMilestone,
            fixture.SecondMilestone,
            fixture.ThirdMilestone);
        foreach (var milestone in fixture.Milestones.Entities)
        {
            await withdrawHandler.Handle(
                new WithdrawMilestoneCommand(
                    fixture.ContractId,
                    milestone.MilestonesId,
                    fixture.FreelancerUserId),
                CancellationToken.None);
        }

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Null(fixture.Contract.CompletedAt);
        Assert.Equal(800_000m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(200_000m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(0m, fixture.FreelancerWallet.AvailableTokens);
        Assert.Equal(800_000m, fixture.FreelancerWallet.WithdrawableTokens);

        var result = await endProjectHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Completed, result.ContractStatus);
        Assert.Equal(200_000m, result.ReleasedAmountVnd);
        Assert.Equal(200_000m, result.ReleasedTokens);
        Assert.Equal(1_000_000m, result.EscrowReleasedAmountVnd);
        Assert.Equal(fixture.Now.AddMinutes(6), fixture.Contract.CompletedAt);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(1_000_000m, fixture.FreelancerWallet.WithdrawableTokens);

        var claim = await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(0m, claim.ReleasedAmountVnd);
        Assert.Equal(0m, claim.ReleasedTokens);
        Assert.True(claim.AlreadyClaimed);
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(milestone.Amount, milestone.ReleasedAmount));
        Assert.Equal((int)ContractEscrowStatus.Released, fixture.Escrow.Status);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(1_000_000m, fixture.FreelancerWallet.WithdrawableTokens);
        Assert.Equal(12, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(6, fixture.EscrowTransactions.Entities.Count);
        Assert.Contains(realtime.ConversationEvents, evt => evt.EventName == "ContractCompleted");
        Assert.Contains(realtime.UsersEvents, evt => evt.EventName == "ContractCompleted");
        Assert.DoesNotContain(realtime.ConversationEvents, evt => evt.EventName == "FinalPayoutClaimed");
        Assert.DoesNotContain(realtime.UsersEvents, evt => evt.EventName == "FinalPayoutClaimed");
        await notifications.Received(1).CreateNotificationAsync(
            fixture.FreelancerUserId,
            NotificationType.ReviewRequested,
            Arg.Any<string>(),
            Arg.Any<string>(),
            fixture.ContractId,
            nameof(Contract),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EndProject_IsIdempotentAfterCompletion()
    {
        var fixture = new MilestoneWorkflowFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            realtime,
            new NoopNotificationService());
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
        Assert.Equal(6, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(1_000_000m, fixture.FreelancerWallet.WithdrawableTokens);

        await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        var claimRetry = await claimHandler.Handle(
            new ClaimFinalPayoutCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.True(claimRetry.AlreadyClaimed);
        Assert.Equal(0m, claimRetry.ReleasedAmountVnd);
        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(1_000_000m, fixture.FreelancerWallet.WithdrawableTokens);
    }

    [Fact]
    public async Task EndProject_RequiresOwningClientAndApprovedMilestones()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            new CapturingChatRealtimeNotifier(),
            new NoopNotificationService());

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
        Assert.Equal(1_000_000m, fixture.ClientWallet.HeldTokens);
    }

    [Fact]
    public async Task ClaimFinalPayout_RequiresSelectedFreelancer()
    {
        var fixture = new MilestoneWorkflowFixture();
        var realtime = new CapturingChatRealtimeNotifier();
        var endHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            realtime,
            new NoopNotificationService());
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

        Assert.Equal(0m, fixture.ClientWallet.HeldTokens);
        Assert.Equal(6, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(0m, fixture.ClientWallet.AvailableTokens);
    }

    [Fact]
    public async Task EndProject_RejectsWhenClientHeldBalanceIsInsufficient()
    {
        var fixture = new MilestoneWorkflowFixture();
        var handler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(5)),
            new CapturingChatRealtimeNotifier(),
            new NoopNotificationService());
        fixture.ApproveMilestone(fixture.FirstMilestone);
        fixture.ApproveMilestone(fixture.SecondMilestone);
        fixture.ApproveMilestone(fixture.ThirdMilestone);
        fixture.ClientWallet.HeldTokens = 0m;

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None));

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(0m, milestone.ReleasedAmount));
        Assert.Equal(0m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrow.Status);
        Assert.Empty(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);
        Assert.Equal(0m, fixture.FreelancerWallet.AvailableTokens);
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
            FirstWorkItem = CreateCompletedWorkItem(FirstMilestoneId, "Milestone 1 work");
            SecondWorkItem = CreateCompletedWorkItem(SecondMilestoneId, "Milestone 2 work");
            ThirdWorkItem = CreateCompletedWorkItem(ThirdMilestoneId, "Milestone 3 work");
            FirstMilestone.WorkItems.Add(FirstWorkItem);
            SecondMilestone.WorkItems.Add(SecondWorkItem);
            ThirdMilestone.WorkItems.Add(ThirdWorkItem);

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" },
                new User { UserId = OutsiderUserId, Role = (int)UserRole.Client, Email = "outsider@example.com", FullName = "Outsider" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(Contract);
            Milestones = Context.AddSet(FirstMilestone, SecondMilestone, ThirdMilestone);
            WorkItems = Context.AddSet(FirstWorkItem, SecondWorkItem, ThirdWorkItem);
            Escrows = Context.AddSet(Escrow);
            // Contract/escrow amounts are G-coin: the client holds the 1,000,000 G-coin
            // escrow directly (no VND -> token division).
            ClientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = ClientUserId,
                AvailableTokens = 0m,
                HeldTokens = 1_000_000m,
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
        public TestDbSet<ContractWorkItem> WorkItems { get; }
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
        public ContractWorkItem FirstWorkItem { get; }
        public ContractWorkItem SecondWorkItem { get; }
        public ContractWorkItem ThirdWorkItem { get; }

        public UserWallet FreelancerWallet =>
            Wallets.Entities.Single(wallet => wallet.UserId == FreelancerUserId);

        public void ApproveMilestone(Milestone milestone)
        {
            milestone.Status = (int)MilestoneStatus.Approved;
            milestone.SubmittedAt = Now;
            milestone.ApprovedAt = Now;
        }

        public async Task ApproveThroughWorkflowAsync(params Milestone[] milestones)
        {
            var handler = new ApproveMilestoneCommandHandler(
                Context,
                new FixedDateTimeService(Now.AddMinutes(2)));

            foreach (var milestone in milestones)
            {
                milestone.Status = (int)MilestoneStatus.Submitted;
                milestone.SubmittedAt = Now;
                await handler.Handle(
                    new ApproveMilestoneCommand(ContractId, milestone.MilestonesId, ClientUserId),
                    CancellationToken.None);
            }
        }

        private ContractWorkItem CreateCompletedWorkItem(Guid milestoneId, string title) => new()
        {
            ContractWorkItemId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            Title = title,
            Description = $"Complete {title.ToLowerInvariant()}.",
            Deliverables = "Verified deliverable",
            EstimatedDuration = "1 week",
            OrderIndex = 0,
            Status = (int)ContractWorkItemStatus.Completed,
            CompletedAt = Now,
            CreatedAt = Now,
            UpdatedAt = Now
        };
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
        public string? UploadedFileName { get; private set; }
        public string? UploadedContentType { get; private set; }
        public byte[]? UploadedContent { get; private set; }
        public int UploadCount { get; private set; }

        public async Task<string> UploadFileAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            string folder,
            CancellationToken cancellationToken = default)
        {
            using var uploadedContent = new MemoryStream();
            await fileStream.CopyToAsync(uploadedContent, cancellationToken);

            UploadedFileName = fileName;
            UploadedContentType = contentType;
            UploadedContent = uploadedContent.ToArray();
            UploadCount++;

            return $"https://test-storage.com/{folder}/{fileName}";
        }

        public Task<string> UploadPrivateFileAsync(Stream fileStream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
            => UploadFileAsync(fileStream, fileName, contentType, folder, cancellationToken);

        public Task DeleteFileAsync(
            string fileUrl,
            string expectedFolder,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> GetPrivateDownloadUrlAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(storageKey);

        public Task DeletePrivateFileAsync(string storageKey, string contentType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableReadStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
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
            new SubmitMilestoneFile(
                new MemoryStream(ValidPdfContent),
                "testfile.pdf",
                "application/pdf",
                ValidPdfContent.Length));

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
        Assert.Equal(ValidPdfContent.Length, attachments[0].FileSize);
        Assert.Equal((int)MilestoneSubmissionSourceType.File, attachments[0].SourceType);
        Assert.Equal("application/pdf", attachments[0].MimeType);
        Assert.Equal(ValidPdfContent, mediaService.UploadedContent);
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
                File: CreateSubmissionFile("milestone-2.zip")),
            CancellationToken.None));

        Assert.Equal((int)MilestoneStatus.Pending, fixture.SecondMilestone.Status);
        Assert.Null(fixture.SecondMilestone.StartedAt);
        Assert.Null(fixture.SecondMilestone.SubmittedAt);
    }

    [Fact]
    public async Task SubmitMilestone_RequiresAValidFile()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;
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
                    File: new SubmitMilestoneFile(
                        new MemoryStream(new byte[] { 1 }),
                        "huge.zip",
                        "application/zip",
                        100 * 1024 * 1024 + 1)),
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitMilestone_NormalizesFileNameAndDeclaredContentTypeBeforeStorage()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        var response = await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                File: new SubmitMilestoneFile(
                    new MemoryStream(ValidPdfContent),
                    @"..\reports\<final>:draft?.pdf",
                    "APPLICATION/PDF; charset=binary",
                    ValidPdfContent.Length)),
            CancellationToken.None);

        var attachment = Assert.Single(response.Attachments);
        Assert.Equal("_final__draft_.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.MimeType);
        Assert.Equal("_final__draft_.pdf", mediaService.UploadedFileName);
        Assert.Equal("application/pdf", mediaService.UploadedContentType);
    }

    [Fact]
    public async Task SubmitMilestone_AcceptsZipBasedOfficeDocument()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                File: new SubmitMilestoneFile(
                    new MemoryStream(ValidZipContent),
                    "handoff.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ValidZipContent.Length)),
            CancellationToken.None);

        Assert.Equal(1, mediaService.UploadCount);
        Assert.Equal(ValidZipContent, mediaService.UploadedContent);
    }

    [Fact]
    public async Task SubmitMilestone_RejectsDisallowedMismatchedAndEmptyFilesBeforeUpload()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        var rejectedFiles = new[]
        {
            new SubmitMilestoneFile(
                new MemoryStream([0x4D, 0x5A]),
                "payload.exe",
                "application/x-msdownload",
                2),
            new SubmitMilestoneFile(
                new MemoryStream(ValidPdfContent),
                "deliverable.pdf",
                "application/zip",
                ValidPdfContent.Length),
            new SubmitMilestoneFile(
                new MemoryStream(ValidZipContent),
                "spoofed.pdf",
                "application/pdf",
                ValidZipContent.Length),
            new SubmitMilestoneFile(
                new MemoryStream(),
                "empty.txt",
                "text/plain",
                1),
            new SubmitMilestoneFile(
                new MemoryStream([0x00]),
                "binary.txt",
                "text/plain",
                1)
        };

        foreach (var file in rejectedFiles)
        {
            await Assert.ThrowsAsync<BadRequestException>(() =>
                submitHandler.Handle(
                    new SubmitMilestoneCommand(
                        fixture.ContractId,
                        fixture.FirstMilestoneId,
                        fixture.FreelancerUserId,
                        File: file),
                    CancellationToken.None));
        }

        Assert.Equal(0, mediaService.UploadCount);
        Assert.Equal((int)MilestoneStatus.InProgress, fixture.FirstMilestone.Status);
        Assert.Empty(fixture.Context.Set<MilestoneAttachment>().ToList());
    }

    [Fact]
    public async Task SubmitMilestone_PreservesHeaderForNonSeekableUploadStream()
    {
        var fixture = new MilestoneWorkflowFixture();
        var mediaService = new TestMediaService();
        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            mediaService);
        byte[] content = [.. ValidPdfContent, 0x01, 0x02, 0x03];

        fixture.FirstMilestone.Status = (int)MilestoneStatus.InProgress;

        await submitHandler.Handle(
            new SubmitMilestoneCommand(
                fixture.ContractId,
                fixture.FirstMilestoneId,
                fixture.FreelancerUserId,
                File: new SubmitMilestoneFile(
                    new NonSeekableReadStream(content),
                    "non-seekable.pdf",
                    "application/pdf",
                    content.Length)),
            CancellationToken.None);

        Assert.Equal(content, mediaService.UploadedContent);
    }
}
