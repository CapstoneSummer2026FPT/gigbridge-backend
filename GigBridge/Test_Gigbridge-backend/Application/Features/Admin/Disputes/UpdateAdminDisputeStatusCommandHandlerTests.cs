using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.Admin.Disputes.UpdateStatus.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.Disputes;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Disputes;

/// <summary>
/// Regression coverage for the dispute-chat-locks-too-early bug: the dispute conversation
/// must only ever lock when DisputeStatus explicitly transitions to Closed here — never as
/// a side effect of resolving the dispute (see ResolveAdminDisputeCommandHandlerTests).
/// </summary>
public sealed class UpdateAdminDisputeStatusCommandHandlerTests
{
    [Fact]
    public async Task UpdateStatus_WaitingAdminToInProgress_KeepsConversationActive()
    {
        var fixture = new DisputeStatusFixture();
        fixture.Dispute.Status = (int)DisputeStatus.WaitingAdmin;
        var handler = fixture.CreateHandler();

        await handler.Handle(
            new UpdateAdminDisputeStatusCommand(fixture.DisputeId, fixture.AdminUserId, DisputeStatus.InProgress),
            CancellationToken.None);

        Assert.Equal((int)DisputeStatus.InProgress, fixture.Dispute.Status);
        Assert.Equal((int)ConversationStatus.Active, fixture.DisputeConversation.Status);
    }

    [Fact]
    public async Task UpdateStatus_ResolvedToClosed_LocksTheDisputeConversation()
    {
        var fixture = new DisputeStatusFixture();
        fixture.Dispute.Status = (int)DisputeStatus.Resolved;
        var handler = fixture.CreateHandler();

        await handler.Handle(
            new UpdateAdminDisputeStatusCommand(fixture.DisputeId, fixture.AdminUserId, DisputeStatus.Closed),
            CancellationToken.None);

        Assert.Equal((int)DisputeStatus.Closed, fixture.Dispute.Status);
        Assert.Equal((int)ConversationStatus.Closed, fixture.DisputeConversation.Status);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransitionFromInProgressToClosed_ThrowsAndLeavesConversationActive()
    {
        var fixture = new DisputeStatusFixture();
        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateAdminDisputeStatusCommand(fixture.DisputeId, fixture.AdminUserId, DisputeStatus.Closed),
            CancellationToken.None));

        Assert.Equal((int)DisputeStatus.InProgress, fixture.Dispute.Status);
        Assert.Equal((int)ConversationStatus.Active, fixture.DisputeConversation.Status);
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }

    private sealed class DisputeStatusFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc);
        public CapturingChatRealtimeNotifier Realtime { get; } = new();

        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid DisputeId { get; } = Guid.NewGuid();

        public Dispute Dispute { get; }
        public Conversation DisputeConversation { get; }

        public DisputeStatusFixture()
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

            var milestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Disputed milestone",
                Amount = 100m,
                Status = (int)MilestoneStatus.Disputed,
                SortOrder = 0,
                CreatedAt = Now
            };

            var contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = jobPost.JobPostsId,
                ClientProfilesId = clientProfile.ClientProfilesId,
                FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
                Title = "Test contract",
                TotalBudget = 100m,
                Status = (int)ContractStatus.Disputed,
                CreatedAt = Now,
                ClientProfiles = clientProfile,
                FreelancerProfiles = freelancerProfile,
                Milestones = new List<Milestone> { milestone }
            };

            Dispute = new Dispute
            {
                DisputesId = DisputeId,
                ContractsId = ContractId,
                InitiatorId = FreelancerUserId,
                RespondentId = ClientUserId,
                MilestonesId = milestone.MilestonesId,
                Reason = "Payment dispute",
                Status = (int)DisputeStatus.InProgress,
                AssignedAdminId = AdminUserId,
                AssignedAt = Now,
                CreatedAt = Now,
                Contracts = contract,
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

            Context.AddSet(clientUser, freelancerUser, adminUser);
            Context.AddSet(clientProfile);
            Context.AddSet(freelancerProfile);
            Context.AddSet(jobPost);
            Context.AddSet(contract);
            Context.AddSet(milestone);
            Context.AddSet(Dispute);
            Context.AddSet(DisputeConversation);
            Context.AddSet<ConversationParticipant>();
            Context.AddSet<Message>();
            Context.AddSet<DisputeEvidence>();
            Context.AddSet<DisputeMilestoneDecision>();
            Context.AddSet<DisputePenalty>();
            Context.AddSet<ContractEscrow>();
            Context.AddSet<WalletTransaction>();
            Context.AddSet<AdminAuditLog>();
        }

        public UpdateAdminDisputeStatusCommandHandler CreateHandler() => new(
            Context,
            new FixedDateTimeService(Now.AddMinutes(5)),
            Realtime,
            new NoopNotificationService(),
            NullLogger<UpdateAdminDisputeStatusCommandHandler>.Instance);
    }
}
