using Application.Common.Interfaces.Time;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.ESign;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Escrow;

/// <summary>
/// Escrow funding used to announce itself only to the SignalR conversation group, which a
/// client joins by invoking ChatHub.JoinConversation. The contract detail page never joins
/// that group, so the freelancer's "waiting for escrow funding" card stayed stale until a
/// manual refresh. These tests pin the per-user delivery that fixes it.
/// </summary>
public sealed class FundContractEscrowRealtimeTests
{
    [Fact]
    public async Task FundEscrow_SendsEscrowFundedToBothParticipantsPerUserNotOnlyToTheConversationGroup()
    {
        var fixture = new EscrowRealtimeFixture();
        var notifier = new CapturingChatRealtimeNotifier();

        var result = await fixture.CreateHandler(notifier).Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, result.ContractStatus);

        var escrowFunded = Assert.Single(notifier.UsersEvents.Where(e => e.EventName == "EscrowFunded"));
        Assert.Contains(fixture.ClientUserId, escrowFunded.UserIds);
        Assert.Contains(fixture.FreelancerUserId, escrowFunded.UserIds);
    }

    [Fact]
    public async Task FundEscrow_SendsWorkspaceOpenedPerUserSoTheContractPageReceivesItWithoutJoiningTheGroup()
    {
        var fixture = new EscrowRealtimeFixture();
        var notifier = new CapturingChatRealtimeNotifier();

        await fixture.CreateHandler(notifier).Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        var workspaceOpened = Assert.Single(notifier.UsersEvents.Where(e => e.EventName == "WorkspaceOpened"));
        Assert.Contains(fixture.ClientUserId, workspaceOpened.UserIds);
        Assert.Contains(fixture.FreelancerUserId, workspaceOpened.UserIds);

        // The negotiation thread became the workspace, so the inbox has to re-categorise it.
        Assert.Contains(notifier.UsersEvents, e => e.EventName == "ConversationUpdated");
    }

    [Fact]
    public async Task FundEscrow_PushesThePersistedSystemMessageSoTheChatThreadUpdatesLive()
    {
        var fixture = new EscrowRealtimeFixture();
        var notifier = new CapturingChatRealtimeNotifier();

        await fixture.CreateHandler(notifier).Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        var persisted = Assert.Single(fixture.Context.Set<Message>());
        Assert.Equal("Escrow funded. Contract is now active.", persisted.Content);

        Assert.Contains(notifier.UsersEvents, e => e.EventName == "ReceiveMessage");
        Assert.Contains(
            notifier.ConversationEvents,
            e => e.EventName == "ReceiveMessage" && e.ConversationId == fixture.ConversationId);
    }

    [Fact]
    public async Task FundEscrow_AlreadyFundedEscrowStillAnnouncesTheTransitionInsteadOfCompletingSilently()
    {
        var fixture = new EscrowRealtimeFixture();
        var escrow = fixture.Escrow;
        escrow.Status = (int)ContractEscrowStatus.Funded;
        escrow.FundedAmount = escrow.RequiredAmount;
        escrow.FundedAt = fixture.Now;

        var notifier = new CapturingChatRealtimeNotifier();

        var result = await fixture.CreateHandler(notifier).Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        // This branch performs a real PendingEscrow -> Active transition, so it must not be silent.
        Assert.Equal((int)ContractStatus.Active, result.ContractStatus);
        var escrowFunded = Assert.Single(notifier.UsersEvents.Where(e => e.EventName == "EscrowFunded"));
        Assert.Contains(fixture.FreelancerUserId, escrowFunded.UserIds);
    }

    [Fact]
    public async Task FundEscrow_ContractAlreadyActiveOnEntryStaysSilentBecauseNoStateChanges()
    {
        var fixture = new EscrowRealtimeFixture();
        fixture.Contract.Status = (int)ContractStatus.Active;
        fixture.Escrow.Status = (int)ContractEscrowStatus.Funded;

        var notifier = new CapturingChatRealtimeNotifier();

        await fixture.CreateHandler(notifier).Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Empty(notifier.UsersEvents);
        Assert.Empty(notifier.ConversationEvents);
    }

    private sealed class EscrowRealtimeFixture
    {
        public EscrowRealtimeFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Realtime contract",
                TotalBudget = 1_000m,
                Status = (int)ContractStatus.PendingEscrow,
                CreatedAt = Now
            };

            Conversation = new Conversation
            {
                ConversationsId = ConversationId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = JobPostId,
                ContractsId = ContractId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };

            Escrow = new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000m,
                FundedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.PendingFunding,
                CreatedAt = Now
            };

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Realtime job",
                Description = "Build it",
                Status = 1,
                CreatedAt = Now
            });
            Context.AddSet(Contract);
            Context.AddSet(Conversation);
            Context.AddSet(Escrow);

            // The nav property has to be wired by hand: the in-memory sets run plain LINQ, and
            // the participant lookup filters on participant.Conversations.ContractsId.
            Context.AddSet(
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationId,
                    UserId = ClientUserId,
                    ParticipantRole = (int)ParticipantRole.Client,
                    JoinedAt = Now,
                    Conversations = Conversation
                },
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationId,
                    UserId = FreelancerUserId,
                    ParticipantRole = (int)ParticipantRole.Freelancer,
                    JoinedAt = Now,
                    Conversations = Conversation
                });

            Context.AddSet(new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                DocumentCode = "GB-REALTIME",
                Status = (int)ESignDocumentStatus.FullySigned,
                FinalizedAt = Now,
                CreatedAt = Now
            });

            Context.AddSet(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 1_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            });

            // 1,000 escrow + 1% (10) service fee.
            Context.AddSet(new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = ClientUserId,
                AvailableTokens = 5_000m,
                HeldTokens = 0m,
                CreatedAt = Now
            });

            Context.AddSet<Message>();
            Context.AddSet<WalletTransaction>();
            Context.AddSet<EscrowTransaction>();
            Context.AddSet<Proposal>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public Conversation Conversation { get; }
        public ContractEscrow Escrow { get; }

        public FundContractEscrowCommandHandler CreateHandler(CapturingChatRealtimeNotifier notifier) =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopNotificationService(),
                notifier,
                new CapturingUserAuditLogService(),
                NullLogger<FundContractEscrowCommandHandler>.Instance);

        private sealed class FixedDateTimeService(DateTime utcNow) : IDateTimeService
        {
            public DateTime UtcNow { get; } = utcNow;
        }
    }
}
