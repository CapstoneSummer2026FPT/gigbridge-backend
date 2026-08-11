using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Resolve.Commands;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Disputes;

/// <summary>
/// Regression coverage for the Keep/Resume Contract milestone-accounting bug and the
/// dispute-chat-locks-too-early bug: resolving a dispute must (a) leave the dispute
/// conversation writable until an explicit Close action, and (b) keep Milestone.Amount,
/// Milestone.RefundedAmount and escrow.FundedAmount consistent so EndProject/FinalPayout
/// never double-count or permanently block on a dispute-resolved milestone.
/// </summary>
public sealed class ResolveAdminDisputeCommandHandlerTests
{
    [Fact]
    public async Task Resolve_Resume_Accepted_FinalizesMilestoneAndKeepsConversationWritable()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(
            fixture.BuildCommand(
                new AdminMilestoneAllocationInput(
                    fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Accepted, 100m, 0m, 0m, null),
                AdminContractAction.Resume),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal((int)MilestoneStatus.Completed, fixture.DisputedMilestone.Status);
        Assert.Equal(100m, fixture.DisputedMilestone.ReleasedAmount);
        Assert.Equal(0m, fixture.DisputedMilestone.RefundedAmount);
        Assert.Equal(150m, fixture.Escrow.FundedAmount);
        Assert.Equal(100m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal((int)DisputeStatus.Resolved, fixture.Dispute.Status);

        // Bug 1 regression: the dispute conversation must stay Active/writable after Resolve.
        Assert.Equal((int)ConversationStatus.Active, fixture.DisputeConversation.Status);
        Assert.Null(fixture.DisputeConversation.DeletedAt);

        Assert.Contains(fixture.Realtime.UsersEvents, evt =>
            evt.EventName == "ContractUpdated" &&
            evt.UserIds.Contains(fixture.ClientUserId) &&
            evt.UserIds.Contains(fixture.FreelancerUserId));
    }

    [Fact]
    public async Task Resolve_Resume_PartiallyAccepted_KeepsEscrowAndMilestoneTotalsInSyncAndAllowsEndProject()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand(
                new AdminMilestoneAllocationInput(
                    fixture.DisputedMilestoneId, DisputeMilestoneOutcome.PartiallyAccepted, 60m, 40m, 0m,
                    "Partial delivery accepted."),
                AdminContractAction.Resume),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Completed, fixture.DisputedMilestone.Status);
        Assert.Equal(60m, fixture.DisputedMilestone.ReleasedAmount);
        Assert.Equal(40m, fixture.DisputedMilestone.RefundedAmount);
        Assert.Equal(110m, fixture.Escrow.FundedAmount);
        Assert.Equal(60m, fixture.Escrow.ReleasedAmount);

        // Core regression: EndProject must succeed once every milestone is resolved, even
        // though this dispute included a refund. Before the fix, escrow.FundedAmount no
        // longer matched Σ Milestone.Amount and this threw BadRequestException forever.
        var endProjectHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(10)),
            fixture.Realtime,
            new NoopNotificationService());

        var result = await endProjectHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Completed, result.ContractStatus);
        // Sibling milestone (50, untouched by the dispute) pays out in full; the disputed
        // milestone contributes nothing further (60 already released, 40 already refunded).
        Assert.Equal(50m, result.ReleasedAmountVnd);
        Assert.Equal(110m, fixture.FreelancerWallet.WithdrawableTokens);
    }

    [Fact]
    public async Task Resolve_Resume_Cancelled_FinalizesMilestoneInsteadOfDeadEndStatus()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand(
                new AdminMilestoneAllocationInput(
                    fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Cancelled, 0m, 100m, 0m,
                    "Milestone voided; full refund to client."),
                AdminContractAction.Resume),
            CancellationToken.None);

        // Before the fix this landed on MilestoneStatus.Cancelled, a status nothing ever
        // transitions out of, permanently blocking EndProjectCommandHandler for the contract.
        Assert.Equal((int)MilestoneStatus.Completed, fixture.DisputedMilestone.Status);
        Assert.Equal(100m, fixture.DisputedMilestone.RefundedAmount);
        Assert.Equal(50m, fixture.Escrow.FundedAmount);

        var endProjectHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(10)),
            fixture.Realtime,
            new NoopNotificationService());

        var result = await endProjectHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Completed, result.ContractStatus);
    }

    [Fact]
    public async Task Resolve_Resume_Rejected_ReturnsMilestoneToInProgressAndPreventsOverWithdrawalAfterRefund()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand(
                new AdminMilestoneAllocationInput(
                    fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                    "Work rejected; full refund to client."),
                AdminContractAction.Resume),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.DisputedMilestone.Status);
        Assert.Equal(100m, fixture.DisputedMilestone.RefundedAmount);
        Assert.Equal(0m, fixture.DisputedMilestone.ReleasedAmount);

        // Freelancer redoes the work and gets re-approved through the normal flow. The 80%
        // withdrawal cap must be based on what's actually left in escrow (Amount -
        // RefundedAmount), not the stale original Amount, or the freelancer could withdraw
        // against money that was already refunded to the client.
        fixture.DisputedMilestone.Status = (int)MilestoneStatus.Approved;
        fixture.DisputedMilestone.ApprovedAt = fixture.Now;

        var withdrawHandler = new WithdrawMilestoneCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(10)));

        await Assert.ThrowsAsync<ConflictException>(() => withdrawHandler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.DisputedMilestoneId, fixture.FreelancerUserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_Terminate_DoesNotCloseTheDisputeConversation()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand(
                new[]
                {
                    new AdminMilestoneAllocationInput(
                        fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                        "Contract terminated; refund remaining escrow."),
                    new AdminMilestoneAllocationInput(
                        fixture.SiblingMilestoneId, DisputeMilestoneOutcome.Rejected, 0m, 50m, 0m,
                        "Contract terminated; refund remaining escrow."),
                },
                AdminContractAction.Terminate),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contract.Status);
        Assert.Equal((int)DisputeStatus.Resolved, fixture.Dispute.Status);

        // Bug 1 regression: even Terminate must not lock the dispute conversation early —
        // only an explicit UpdateAdminDisputeStatusCommandHandler(Closed) call should.
        Assert.Equal((int)ConversationStatus.Active, fixture.DisputeConversation.Status);
        Assert.Null(fixture.DisputeConversation.DeletedAt);
    }

    [Fact]
    public async Task Resolve_CalledTwice_ThrowsConflictOnSecondAttempt()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();
        var command = fixture.BuildCommand(
            new AdminMilestoneAllocationInput(
                fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Accepted, 100m, 0m, 0m, null),
            AdminContractAction.Resume);

        await handler.Handle(command, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    /// <summary>
    /// A contract with two milestones: the disputed one (100, nothing released yet) and an
    /// already-approved sibling (50, untouched by the dispute) used to prove EndProject
    /// works correctly once the disputed milestone is resolved.
    /// </summary>
    private sealed class DisputeResolutionFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        public CapturingChatRealtimeNotifier Realtime { get; } = new();

        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid DisputedMilestoneId { get; } = Guid.NewGuid();
        public Guid SiblingMilestoneId { get; } = Guid.NewGuid();
        public Guid DisputeId { get; } = Guid.NewGuid();

        public Contract Contract { get; }
        public ContractEscrow Escrow { get; }
        public Milestone DisputedMilestone { get; }
        public Milestone SiblingMilestone { get; }
        public Dispute Dispute { get; }
        public Conversation DisputeConversation { get; }
        public UserWallet ClientWallet { get; }

        public UserWallet FreelancerWallet =>
            Context.Set<UserWallet>().Single(wallet => wallet.UserId == FreelancerUserId);

        public DisputeResolutionFixture()
        {
            var clientUser = new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client", IsActive = true };
            var freelancerUser = new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer", IsActive = true };
            var adminUser = new User { UserId = AdminUserId, Role = (int)UserRole.Admin, Email = "admin@example.com", FullName = "Admin", IsActive = true };

            var clientProfile = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = ClientUserId, User = clientUser, CreatedAt = Now };
            var freelancerProfile = new FreelancerProfile { FreelancerProfilesId = Guid.NewGuid(), UserId = FreelancerUserId, User = freelancerUser, CreatedAt = Now };

            var jobPost = new JobPost
            {
                JobPostsId = Guid.NewGuid(),
                ClientProfilesId = clientProfile.ClientProfilesId,
                Title = "Test job",
                Description = "Test job description",
                Status = 1,
                CreatedAt = Now
            };

            DisputedMilestone = new Milestone
            {
                MilestonesId = DisputedMilestoneId,
                ContractsId = ContractId,
                Title = "Disputed milestone",
                Amount = 100m,
                Status = (int)MilestoneStatus.Disputed,
                SortOrder = 0,
                CreatedAt = Now
            };
            SiblingMilestone = new Milestone
            {
                MilestonesId = SiblingMilestoneId,
                ContractsId = ContractId,
                Title = "Sibling milestone",
                Amount = 50m,
                Status = (int)MilestoneStatus.Approved,
                SortOrder = 1,
                CreatedAt = Now,
                ApprovedAt = Now
            };

            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = clientProfile.ClientProfilesId,
                FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
                Title = "Test contract",
                TotalBudget = 150m,
                Status = (int)ContractStatus.Disputed,
                CreatedAt = Now,
                ClientProfiles = clientProfile,
                FreelancerProfiles = freelancerProfile,
                Milestones = new List<Milestone> { DisputedMilestone, SiblingMilestone }
            };

            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 150m,
                FundedAmount = 150m,
                ReleasedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                DepositedTokens = 150m,
                EarnedTokens = 0m,
                Status = (int)ContractEscrowStatus.Funded,
                CreatedAt = Now,
                FundedAt = Now
            };

            Dispute = new Dispute
            {
                DisputesId = DisputeId,
                ContractsId = ContractId,
                InitiatorId = FreelancerUserId,
                RespondentId = ClientUserId,
                MilestonesId = DisputedMilestoneId,
                Reason = "Payment dispute",
                Status = (int)DisputeStatus.InProgress,
                AssignedAdminId = AdminUserId,
                AssignedAt = Now,
                CreatedAt = Now,
                Contracts = Contract,
                Initiator = freelancerUser
            };

            DisputeConversation = new Conversation
            {
                ConversationsId = Guid.NewGuid(),
                ConversationType = (int)ConversationType.Dispute,
                Title = "Dispute chat",
                ContractsId = ContractId,
                DisputesId = DisputeId,
                CreatedByUserId = FreelancerUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };

            ClientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = ClientUserId,
                AvailableTokens = 0m,
                HeldTokens = 150m,
                CreatedAt = Now
            };
            var freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = FreelancerUserId,
                AvailableTokens = 0m,
                WithdrawableTokens = 0m,
                CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerUser, adminUser);
            Context.AddSet(clientProfile);
            Context.AddSet(freelancerProfile);
            Context.AddSet(jobPost);
            Context.AddSet(Contract);
            Context.AddSet(DisputedMilestone, SiblingMilestone);
            Context.AddSet(Escrow);
            Context.AddSet(Dispute);
            Context.AddSet(DisputeConversation);
            Context.AddSet(ClientWallet, freelancerWallet);
            Context.AddSet<WalletTransaction>();
            Context.AddSet<EscrowTransaction>();
            Context.AddSet<DisputeMilestoneDecision>();
            Context.AddSet<DisputePenalty>();
            Context.AddSet<AdminAuditLog>();
            Context.AddSet<DisputeEvidence>();
            Context.AddSet<Message>();
            Context.AddSet<UserViolation>();
        }

        public ResolveAdminDisputeCommandHandler CreateHandler() => new(
            Context,
            new FixedDateTimeService(Now.AddMinutes(5)),
            Realtime,
            NullLogger<ResolveAdminDisputeCommandHandler>.Instance,
            Substitute.For<IUserAccountStatusService>(),
            Substitute.For<IUserEloService>());

        public ResolveAdminDisputeCommand BuildCommand(
            AdminMilestoneAllocationInput allocation,
            AdminContractAction contractAction) =>
            BuildCommand(new[] { allocation }, contractAction);

        public ResolveAdminDisputeCommand BuildCommand(
            IReadOnlyList<AdminMilestoneAllocationInput> allocations,
            AdminContractAction contractAction) => new(
            DisputeId,
            AdminUserId,
            DisputeResolution.Split,
            "Resolution note.",
            null,
            allocations,
            contractAction,
            new AdminViolationInput(false, null, null, null),
            new AdminViolationInput(false, null, null, null));
    }
}
