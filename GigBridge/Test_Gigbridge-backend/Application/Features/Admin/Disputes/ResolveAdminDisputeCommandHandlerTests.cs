using Application.Common.Exceptions;
using Application.Common.InternalServices.Accounts.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.Elo.Common.Interfaces;
using Application.Features.Admin.Disputes.Resolve.Commands;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Milestones.Freelancer.RequestUnlock.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Submit.Commands;
using Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.Commands;
using Application.Features.Contracts.WorkItems.Freelancer.Update.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Disputes;
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
    public async Task Resolve_Resume_SelectingFirstMilestone_CompletesItAndKeepsConversationWritable()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(
            fixture.BuildResumeCommand([fixture.DisputedMilestoneId]),
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal((int)MilestoneStatus.Completed, fixture.DisputedMilestone.Status);
        Assert.Equal(100m, fixture.DisputedMilestone.ReleasedAmount);
        Assert.Equal(0m, fixture.DisputedMilestone.RefundedAmount);
        Assert.Equal(150m, fixture.Escrow.FundedAmount);
        Assert.Equal(100m, fixture.Escrow.ReleasedAmount);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal((int)DisputeStatus.Resolved, fixture.Dispute.Status);
        // Not selected — must stay completely untouched.
        Assert.Equal((int)MilestoneStatus.Approved, fixture.SiblingMilestone.Status);
        Assert.Equal(0m, fixture.SiblingMilestone.ReleasedAmount);

        // Bug 1 regression: the dispute conversation must stay Active/writable after Resolve.
        Assert.Equal((int)ConversationStatus.Active, fixture.DisputeConversation.Status);
        Assert.Null(fixture.DisputeConversation.DeletedAt);

        Assert.Contains(fixture.Realtime.UsersEvents, evt =>
            evt.EventName == "ContractUpdated" &&
            evt.UserIds.Contains(fixture.ClientUserId) &&
            evt.UserIds.Contains(fixture.FreelancerUserId));
    }

    [Fact]
    public async Task Resolve_Resume_SelectingBothMilestones_CompletesBothInOrder()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildResumeCommand([fixture.DisputedMilestoneId, fixture.SiblingMilestoneId]),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Completed, fixture.DisputedMilestone.Status);
        Assert.Equal(100m, fixture.DisputedMilestone.ReleasedAmount);
        Assert.Equal((int)MilestoneStatus.Completed, fixture.SiblingMilestone.Status);
        Assert.Equal(50m, fixture.SiblingMilestone.ReleasedAmount);
        Assert.Equal(150m, fixture.Escrow.ReleasedAmount);
        Assert.Equal(fixture.Escrow.FundedAmount, fixture.Escrow.ReleasedAmount);
    }

    [Fact]
    public async Task Resolve_Resume_SkippingTheFirstMilestone_IsRejected()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        // M1 + M3-style skip: selecting only the second milestone without the first.
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            fixture.BuildResumeCommand([fixture.SiblingMilestoneId]),
            CancellationToken.None));

        Assert.Equal("Milestones must be selected sequentially from the top with no gaps.", exception.Message);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.SiblingMilestone.Status);
        Assert.Equal(0m, fixture.SiblingMilestone.ReleasedAmount);
    }

    [Fact]
    public async Task Resolve_Resume_EmptySelection_ProcessesNothingButStillReturnsContractToActive()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(fixture.BuildResumeCommand([]), CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal((int)MilestoneStatus.Disputed, fixture.DisputedMilestone.Status);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.SiblingMilestone.Status);
        Assert.Equal(0m, fixture.Escrow.ReleasedAmount);
        Assert.Empty(fixture.Context.Set<EscrowTransaction>().ToList());
    }

    [Fact]
    public async Task Resolve_Resume_AlreadyCompletedMilestoneWithResidualEscrow_ReleasesResidualAndStaysComplete()
    {
        var fixture = new DisputeResolutionFixture();
        // Sibling is already Complete but only ever had 80% released (20% early-withdrawal
        // cap residual) — must remain selectable and release exactly the residual.
        fixture.SiblingMilestone.Status = (int)MilestoneStatus.Completed;
        fixture.SiblingMilestone.ReleasedAmount = 40m;
        fixture.SiblingMilestone.ApprovedAt = fixture.Now;
        fixture.Escrow.ReleasedAmount = 40m;
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildResumeCommand([fixture.DisputedMilestoneId, fixture.SiblingMilestoneId]),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Completed, fixture.SiblingMilestone.Status);
        Assert.Equal(50m, fixture.SiblingMilestone.ReleasedAmount);
        Assert.Equal(0m, fixture.SiblingMilestone.RefundedAmount);
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
    public async Task Resolve_Terminate_M1M2M3Example_DrainsAllRemainingEscrowAcrossEveryMilestone()
    {
        // M1: already Completed, freelancer already withdrew 80 of 100 (20 still locked).
        // M2: the disputed, unfinished milestone (100 locked, nothing released).
        // M3: untouched/unfinished (100 locked, nothing released).
        // The admin must be able to — and must — explicitly decide all three when choosing
        // "Cancel Contract Immediately," not just M2.
        var fixture = new TerminateThreeMilestoneFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand(new[]
            {
                new AdminMilestoneAllocationInput(
                    fixture.M1Id, DisputeMilestoneOutcome.Accepted, 0m, 20m, 0m,
                    "M1 already completed; remaining 20% held in escrow refunded to client on termination."),
                new AdminMilestoneAllocationInput(
                    fixture.M2Id, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                    "M2 disputed and unfinished; full refund to client."),
                new AdminMilestoneAllocationInput(
                    fixture.M3Id, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                    "M3 never started; full refund to client."),
            }),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contract.Status);
        // No escrow may remain locked anywhere in the now-cancelled contract.
        Assert.Equal(fixture.Escrow.ReleasedAmount, fixture.Escrow.FundedAmount);
        Assert.Equal(20m, fixture.M1.RefundedAmount);
        Assert.Equal(100m, fixture.M2.RefundedAmount);
        Assert.Equal(100m, fixture.M3.RefundedAmount);
        // The admin's explicit "Accepted" choice for M1 keeps it out of InProgress/Cancelled —
        // full discretion means the admin decides this, not the system.
        Assert.Equal((int)MilestoneStatus.Approved, fixture.M1.Status);
    }

    [Fact]
    public async Task Resolve_Terminate_M1M2M3Example_RejectsIfM1OrM3IsOmitted()
    {
        var fixture = new TerminateThreeMilestoneFixture();
        var handler = fixture.CreateHandler();

        // Admin only adjudicates the disputed milestone (M2); M1's remaining 20% and M3's
        // full 100% are left out — the whole termination must be rejected, not partially
        // applied, so escrow can never end up stuck in the cancelled contract.
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            fixture.BuildCommand(new[]
            {
                new AdminMilestoneAllocationInput(
                    fixture.M2Id, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m, "M2 refund."),
            }),
            CancellationToken.None));

        Assert.Equal("An allocation is required for every affected milestone.", exception.Message);
        Assert.Equal((int)ContractStatus.Disputed, fixture.Contract.Status);
    }

    private sealed class TerminateThreeMilestoneFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc);
        public CapturingChatRealtimeNotifier Realtime { get; } = new();

        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid M1Id { get; } = Guid.NewGuid();
        public Guid M2Id { get; } = Guid.NewGuid();
        public Guid M3Id { get; } = Guid.NewGuid();
        public Guid DisputeId { get; } = Guid.NewGuid();

        public Contract Contract { get; }
        public ContractEscrow Escrow { get; }
        public Milestone M1 { get; }
        public Milestone M2 { get; }
        public Milestone M3 { get; }
        public Dispute Dispute { get; }

        public TerminateThreeMilestoneFixture()
        {
            var clientUser = new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client", IsActive = true };
            var freelancerUser = new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer", IsActive = true };
            var adminUser = new User { UserId = AdminUserId, Role = (int)UserRole.Admin, Email = "admin@example.com", FullName = "Admin", IsActive = true };

            var clientProfile = new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = ClientUserId, User = clientUser, CreatedAt = Now };
            var freelancerProfile = new FreelancerProfile { FreelancerProfilesId = Guid.NewGuid(), UserId = FreelancerUserId, User = freelancerUser, CreatedAt = Now };

            var jobPost = new JobPost
            {
                JobPostsId = Guid.NewGuid(), ClientProfilesId = clientProfile.ClientProfilesId,
                Title = "Test job", Description = "Test job description", Status = 1, CreatedAt = Now
            };

            M1 = new Milestone
            {
                MilestonesId = M1Id, ContractsId = ContractId, Title = "M1", Amount = 100m,
                Status = (int)MilestoneStatus.Completed, ReleasedAmount = 80m, SortOrder = 0, CreatedAt = Now, ApprovedAt = Now
            };
            M2 = new Milestone
            {
                MilestonesId = M2Id, ContractsId = ContractId, Title = "M2 (disputed)", Amount = 100m,
                Status = (int)MilestoneStatus.Disputed, SortOrder = 1, CreatedAt = Now
            };
            M3 = new Milestone
            {
                MilestonesId = M3Id, ContractsId = ContractId, Title = "M3", Amount = 100m,
                Status = (int)MilestoneStatus.Pending, SortOrder = 2, CreatedAt = Now
            };

            Contract = new Contract
            {
                ContractsId = ContractId, JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = clientProfile.ClientProfilesId, FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
                Title = "Test contract", TotalBudget = 300m, Status = (int)ContractStatus.Disputed, CreatedAt = Now,
                ClientProfiles = clientProfile, FreelancerProfiles = freelancerProfile,
                Milestones = new List<Milestone> { M1, M2, M3 }
            };

            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(), ContractsId = ContractId,
                RequiredAmount = 300m, FundedAmount = 300m, ReleasedAmount = 80m,
                RequiredPercentage = 1.0m, Currency = "VND",
                DepositedTokens = 220m, EarnedTokens = 0m,
                Status = (int)ContractEscrowStatus.PartiallyReleased, CreatedAt = Now, FundedAt = Now
            };

            Dispute = new Dispute
            {
                DisputesId = DisputeId, ContractsId = ContractId, InitiatorId = FreelancerUserId, RespondentId = ClientUserId,
                MilestonesId = M2Id, Reason = "Payment dispute", Status = (int)DisputeStatus.InProgress,
                AssignedAdminId = AdminUserId, AssignedAt = Now, CreatedAt = Now, Contracts = Contract, Initiator = freelancerUser
            };

            var disputeConversation = new Conversation
            {
                ConversationsId = Guid.NewGuid(), ConversationType = (int)ConversationType.Dispute, Title = "Dispute chat",
                ContractsId = ContractId, DisputesId = DisputeId, CreatedByUserId = FreelancerUserId,
                Status = (int)ConversationStatus.Active, CreatedAt = Now
            };

            var clientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(), UserId = ClientUserId, AvailableTokens = 0m, HeldTokens = 220m, CreatedAt = Now
            };
            var freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(), UserId = FreelancerUserId, AvailableTokens = 0m, WithdrawableTokens = 80m, CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerUser, adminUser);
            Context.AddSet(clientProfile);
            Context.AddSet(freelancerProfile);
            Context.AddSet(jobPost);
            Context.AddSet(Contract);
            Context.AddSet(M1, M2, M3);
            Context.AddSet(Escrow);
            Context.AddSet(Dispute);
            Context.AddSet(disputeConversation);
            Context.AddSet(clientWallet, freelancerWallet);
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
            Context, new FixedDateTimeService(Now.AddMinutes(5)), Realtime,
            NullLogger<ResolveAdminDisputeCommandHandler>.Instance,
            Substitute.For<IUserAccountStatusService>(), Substitute.For<IUserEloService>());

        public ResolveAdminDisputeCommand BuildCommand(IReadOnlyList<AdminMilestoneAllocationInput> allocations) => new(
            DisputeId, AdminUserId, DisputeResolution.Split, "Resolution note.", null,
            allocations, [], AdminContractAction.Terminate,
            new AdminViolationInput(false, null, null, null), new AdminViolationInput(false, null, null, null));
    }

    [Fact]
    public async Task Resolve_Terminate_RejectsWhenALockedSiblingMilestoneIsLeftUnadjudicated()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        // Admin is given full discretion over every locked milestone, not just the disputed
        // one — Terminate must still require an explicit Release/Refund/Penalty decision for
        // the sibling (50 GCoin, nothing released yet), or the request is rejected outright.
        // This is what stops the contract from ending up Cancelled with escrow left stuck.
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            fixture.BuildCommand(
                new AdminMilestoneAllocationInput(
                    fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                    "Contract terminated; refund remaining escrow."),
                AdminContractAction.Terminate),
            CancellationToken.None));

        Assert.Equal("An allocation is required for every affected milestone.", exception.Message);
        // Nothing should have been committed — the sibling's escrow stays exactly as it was.
        Assert.Equal(0m, fixture.SiblingMilestone.RefundedAmount);
        Assert.Equal((int)ContractStatus.Disputed, fixture.Contract.Status);
    }

    [Fact]
    public async Task Resolve_Terminate_AdminExplicitlyDecidesEveryLockedMilestoneAndPreservesApprovedStatus()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();

        // Full admin discretion: the admin explicitly refunds the sibling too, and chooses
        // to keep it Accepted/Approved rather than letting it regress — this is the admin's
        // call to make, and the handler must respect exactly what they pick.
        await handler.Handle(
            fixture.BuildCommand(
                new[]
                {
                    new AdminMilestoneAllocationInput(
                        fixture.DisputedMilestoneId, DisputeMilestoneOutcome.Rejected, 0m, 100m, 0m,
                        "Contract terminated; refund remaining escrow."),
                    new AdminMilestoneAllocationInput(
                        fixture.SiblingMilestoneId, DisputeMilestoneOutcome.Accepted, 0m, 50m, 0m,
                        "Work already accepted; remaining escrow refunded as part of termination."),
                },
                AdminContractAction.Terminate),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contract.Status);
        Assert.Equal(50m, fixture.SiblingMilestone.RefundedAmount);
        Assert.Equal((int)MilestoneStatus.Approved, fixture.SiblingMilestone.Status);
        Assert.Equal(fixture.Escrow.ReleasedAmount, fixture.Escrow.FundedAmount);
    }

    [Fact]
    public async Task Resolve_CalledTwice_ThrowsConflictOnSecondAttempt()
    {
        var fixture = new DisputeResolutionFixture();
        var handler = fixture.CreateHandler();
        var command = fixture.BuildResumeCommand([fixture.DisputedMilestoneId]);

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
            [],
            contractAction,
            new AdminViolationInput(false, null, null, null),
            new AdminViolationInput(false, null, null, null));

        // Defaults every selected milestone to a full release of whatever is currently
        // locked for it — matches the frontend's default suggestion, so tests that don't
        // care about a custom split don't need to spell one out.
        public ResolveAdminDisputeCommand BuildResumeCommand(IReadOnlyList<Guid> selectedMilestoneIds) => new(
            DisputeId,
            AdminUserId,
            DisputeResolution.Split,
            "Resolution note.",
            null,
            selectedMilestoneIds.Select(id =>
            {
                var milestone = id == DisputedMilestoneId ? DisputedMilestone : SiblingMilestone;
                return new AdminMilestoneAllocationInput(
                    id, DisputeMilestoneOutcome.Accepted, milestone.Amount - milestone.ReleasedAmount, 0m, 0m, null);
            }).ToList(),
            selectedMilestoneIds,
            AdminContractAction.Resume,
            new AdminViolationInput(false, null, null, null),
            new AdminViolationInput(false, null, null, null));
    }

    // ---------------------------------------------------------------------
    // Keep Contract must lock the disputed milestone AND advance the workspace
    // to the next milestone. Regression coverage for: "workspace still treats
    // the disputed milestone as active" after Resume.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Resolve_Resume_AdvancesNextPendingMilestoneToInProgress()
    {
        var fixture = new KeepContractAdvanceFixture();
        var handler = fixture.CreateHandler();

        // M3 is the 3rd milestone — selecting it requires selecting the whole top-down
        // prefix {M1, M2, M3}, even though M1/M2 are already Completed (their remaining
        // 20% early-withdrawal-cap residual gets released too).
        await handler.Handle(
            fixture.BuildCommand([fixture.M1.MilestonesId, fixture.M2.MilestonesId, fixture.M3Id]),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Completed, fixture.M3.Status);
        Assert.Equal((int)MilestoneStatus.InProgress, fixture.M4.Status);
        Assert.NotNull(fixture.M4.StartedAt);
    }

    [Fact]
    public async Task Resolve_Resume_LockedMilestoneRejectsSubmitWithdrawUnlockAndWorkItemUpdate()
    {
        var fixture = new KeepContractAdvanceFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand([fixture.M1.MilestonesId, fixture.M2.MilestonesId, fixture.M3Id]),
            CancellationToken.None);

        var submitHandler = new SubmitMilestoneCommandHandler(
            fixture.Context, new FixedDateTimeService(fixture.Now), new CapturingUserAuditLogService());
        await Assert.ThrowsAsync<BadRequestException>(() => submitHandler.Handle(
            new SubmitMilestoneCommand(fixture.ContractId, fixture.M3Id, fixture.FreelancerUserId),
            CancellationToken.None));

        var withdrawHandler = new WithdrawMilestoneCommandHandler(
            fixture.Context, new FixedDateTimeService(fixture.Now));
        await Assert.ThrowsAsync<BadRequestException>(() => withdrawHandler.Handle(
            new WithdrawMilestoneCommand(fixture.ContractId, fixture.M3Id, fixture.FreelancerUserId),
            CancellationToken.None));

        var unlockHandler = new RequestMilestoneUnlockCommandHandler(
            fixture.Context, new FixedDateTimeService(fixture.Now), new NoopNotificationService(),
            new CapturingUserAuditLogService());
        await Assert.ThrowsAsync<BadRequestException>(() => unlockHandler.Handle(
            new RequestMilestoneUnlockCommand(fixture.ContractId, fixture.M3Id, fixture.FreelancerUserId, "Early start"),
            CancellationToken.None));

        var workItemHandler = new UpdateContractWorkItemCommandHandler(
            fixture.Context, new FixedDateTimeService(fixture.Now));
        await Assert.ThrowsAsync<BadRequestException>(() => workItemHandler.Handle(
            new UpdateContractWorkItemCommand(
                fixture.ContractId, fixture.M3Id, Guid.NewGuid(), fixture.FreelancerUserId,
                new UpdateContractWorkItemRequest((int)ContractWorkItemStatus.InProgress, null)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_Resume_DoesNotCreateDuplicateEscrowOrWalletTransactions()
    {
        var fixture = new KeepContractAdvanceFixture();
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand([fixture.M1.MilestonesId, fixture.M2.MilestonesId, fixture.M3Id]),
            CancellationToken.None);

        // AdvanceNextMilestone only flips Milestone.Status/StartedAt — it must not touch
        // escrow or wallet ledgers. One release per selected milestone with a nonzero
        // locked amount: M1 (5 residual), M2 (5 residual), M3 (25, nothing released yet).
        var walletTransactions = fixture.Context.Set<WalletTransaction>().ToList();
        var escrowTransactions = fixture.Context.Set<EscrowTransaction>().ToList();
        Assert.Equal(6, walletTransactions.Count); // client debit + freelancer credit, per release
        Assert.Equal(3, escrowTransactions.Count);
        Assert.Equal(35m, escrowTransactions.Sum(tx => tx.Amount));
        Assert.Equal(70m, walletTransactions.Sum(tx => tx.TokenAmount)); // debit + credit side of each release
    }

    [Fact]
    public async Task Resolve_Resume_FinalMilestoneDispute_CompletesWithNoNextMilestoneAndAllowsEndProject()
    {
        var fixture = new KeepContractAdvanceFixture(disputeIsLastMilestone: true);
        var handler = fixture.CreateHandler();

        await handler.Handle(
            fixture.BuildCommand([fixture.M1.MilestonesId, fixture.M2.MilestonesId, fixture.M3Id]),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.Completed, fixture.M3.Status);
        // M4 wasn't part of this fixture's chain (M3 is the last milestone) — nothing to advance.

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
    public async Task Resolve_Resume_M1ToM4Example_FinalRemainingPayoutIsM4Only()
    {
        var fixture = new KeepContractAdvanceFixture();
        var handler = fixture.CreateHandler();

        // Processing M3 now requires selecting the whole top-down prefix {M1, M2, M3} — so,
        // unlike the old single-milestone-allocation flow, M1/M2's residual 20% is released
        // as part of THIS resolution, not deferred to EndProject.
        await handler.Handle(
            fixture.BuildCommand([fixture.M1.MilestonesId, fixture.M2.MilestonesId, fixture.M3Id]),
            CancellationToken.None);

        Assert.Equal((int)MilestoneStatus.InProgress, fixture.M4.Status);

        // Freelancer finishes M4 through the normal flow; client approves it. This is now the
        // only milestone with money left in escrow — M1/M2/M3 were fully drained above.
        fixture.M4.Status = (int)MilestoneStatus.Approved;
        fixture.M4.ApprovedAt = fixture.Now;

        var endProjectHandler = new EndProjectCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(10)),
            fixture.Realtime,
            new NoopNotificationService());

        var result = await endProjectHandler.Handle(
            new EndProjectCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        // M1/M2/M3 remaining 0 (already released via dispute resolution) + M4 remaining 25 = 25.
        Assert.Equal(25m, result.ReleasedAmountVnd);
    }

    /// <summary>
    /// The M1-M4 scenario from the bug report: M1/M2 already Completed with an 80%-cap
    /// early withdrawal each (20 of 25 released), M3 disputed (still InProgress, nothing
    /// released), M4 pending. Escrow: FundedAmount=100, ReleasedAmount=40 (20+20).
    /// </summary>
    private sealed class KeepContractAdvanceFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        public CapturingChatRealtimeNotifier Realtime { get; } = new();

        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid M3Id { get; } = Guid.NewGuid();
        public Guid DisputeId { get; } = Guid.NewGuid();

        public Contract Contract { get; }
        public ContractEscrow Escrow { get; }
        public Milestone M1 { get; }
        public Milestone M2 { get; }
        public Milestone M3 { get; }
        public Milestone M4 { get; } = null!;
        public Dispute Dispute { get; }

        public KeepContractAdvanceFixture(bool disputeIsLastMilestone = false)
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

            M1 = new Milestone
            {
                MilestonesId = Guid.NewGuid(), ContractsId = ContractId, Title = "M1", Amount = 25m,
                Status = (int)MilestoneStatus.Completed, ReleasedAmount = 20m, SortOrder = 0, CreatedAt = Now, ApprovedAt = Now
            };
            M2 = new Milestone
            {
                MilestonesId = Guid.NewGuid(), ContractsId = ContractId, Title = "M2", Amount = 25m,
                Status = (int)MilestoneStatus.Completed, ReleasedAmount = 20m, SortOrder = 1, CreatedAt = Now, ApprovedAt = Now
            };
            M3 = new Milestone
            {
                MilestonesId = M3Id, ContractsId = ContractId, Title = "M3 (disputed)", Amount = 25m,
                Status = (int)MilestoneStatus.InProgress, SortOrder = 2, CreatedAt = Now
            };

            var milestones = new List<Milestone> { M1, M2, M3 };
            var fundedAmount = 75m;
            if (!disputeIsLastMilestone)
            {
                M4 = new Milestone
                {
                    MilestonesId = Guid.NewGuid(), ContractsId = ContractId, Title = "M4", Amount = 25m,
                    Status = (int)MilestoneStatus.Pending, SortOrder = 3, CreatedAt = Now
                };
                milestones.Add(M4);
                fundedAmount = 100m;
            }

            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = clientProfile.ClientProfilesId,
                FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
                Title = "Test contract",
                TotalBudget = fundedAmount,
                Status = (int)ContractStatus.Disputed,
                CreatedAt = Now,
                ClientProfiles = clientProfile,
                FreelancerProfiles = freelancerProfile,
                Milestones = milestones
            };

            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = fundedAmount,
                FundedAmount = fundedAmount,
                ReleasedAmount = 40m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                DepositedTokens = fundedAmount - 40m,
                EarnedTokens = 0m,
                Status = (int)ContractEscrowStatus.PartiallyReleased,
                CreatedAt = Now,
                FundedAt = Now
            };

            Dispute = new Dispute
            {
                DisputesId = DisputeId,
                ContractsId = ContractId,
                InitiatorId = FreelancerUserId,
                RespondentId = ClientUserId,
                MilestonesId = M3Id,
                Reason = "Payment dispute",
                Status = (int)DisputeStatus.InProgress,
                AssignedAdminId = AdminUserId,
                AssignedAt = Now,
                CreatedAt = Now,
                Contracts = Contract,
                Initiator = freelancerUser
            };

            var disputeConversation = new Conversation
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

            var clientWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(), UserId = ClientUserId,
                AvailableTokens = 0m, HeldTokens = fundedAmount - 40m, CreatedAt = Now
            };
            var freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(), UserId = FreelancerUserId,
                AvailableTokens = 0m, WithdrawableTokens = 40m, CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerUser, adminUser);
            Context.AddSet(clientProfile);
            Context.AddSet(freelancerProfile);
            Context.AddSet(jobPost);
            Context.AddSet(Contract);
            Context.AddSet(milestones.ToArray());
            Context.AddSet(Escrow);
            Context.AddSet(Dispute);
            Context.AddSet(disputeConversation);
            Context.AddSet(clientWallet, freelancerWallet);
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

        public ResolveAdminDisputeCommand BuildCommand(IReadOnlyList<Guid> selectedMilestoneIds) => new(
            DisputeId,
            AdminUserId,
            DisputeResolution.Split,
            "Resolution note.",
            null,
            selectedMilestoneIds.Select(id =>
            {
                var milestone = new[] { M1, M2, M3, M4 }.Single(m => m?.MilestonesId == id);
                return new AdminMilestoneAllocationInput(
                    id, DisputeMilestoneOutcome.Accepted, milestone.Amount - milestone.ReleasedAmount, 0m, 0m, null);
            }).ToList(),
            selectedMilestoneIds,
            AdminContractAction.Resume,
            new AdminViolationInput(false, null, null, null),
            new AdminViolationInput(false, null, null, null));
    }
}
