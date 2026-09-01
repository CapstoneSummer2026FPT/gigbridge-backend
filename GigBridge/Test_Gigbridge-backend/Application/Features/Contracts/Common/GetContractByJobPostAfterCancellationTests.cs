using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Contracts.Cancellation.Common.Cancel.Commands;
using Application.Features.Contracts.Common.GetContractByJobPost.Queries;
using Application.Features.ESign.Common.GetDocumentByContract.Queries;
using Application.Features.ESign.Common.GetDocumentStatusByContract.Queries;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

/// <summary>
/// Regression coverage for a job post being "reapplied" by a DIFFERENT freelancer after an
/// earlier freelancer's Contract was cancelled: GetContractByJobPostQueryHandler must resolve
/// to the live contract for the new freelancer, not the stale Cancelled one left behind by the
/// abandoned attempt (the DB permits both to coexist, since IX_Contracts_JobPostsId excludes
/// Cancelled rows from its uniqueness).
/// </summary>
public class GetContractByJobPostAfterCancellationTests
{
    [Fact]
    public async Task CancelledContract_ReappliedByDifferentFreelancer_ResolvesToLiveContract()
    {
        var fixture = new TwoFreelancerFixture();

        // Freelancer A negotiates and is accepted first.
        fixture.AddFinalOffer(fixture.OfferAId, fixture.ConversationAId, fixture.ProposalAId, fixture.FreelancerAProfileId, 1000m, 1000m);
        var acceptHandler = fixture.CreateRespondFinalOfferHandler();
        var resultA = await acceptHandler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerAUserId,
                new RespondFinalOfferRequest(fixture.OfferAId, FinalOfferResponse.Accept, null)),
            CancellationToken.None);
        Assert.NotNull(resultA.ContractId);
        var contractAId = resultA.ContractId!.Value;

        // The client cancels freelancer A's contract before signing completes.
        var cancelHandler = fixture.CreateCancelHandler();
        await cancelHandler.Handle(
            new CancelContractCommand(contractAId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contracts.Entities.Single(c => c.ContractsId == contractAId).Status);

        // The client negotiates with a DIFFERENT freelancer's proposal on the same job post
        // and accepts it. This is the fresh-create path (different ProposalsId), not the
        // reuse path, so a genuinely new Contract row is created.
        fixture.AddFinalOffer(fixture.OfferBId, fixture.ConversationBId, fixture.ProposalBId, fixture.FreelancerBProfileId, 1500m, 1500m);
        var resultB = await acceptHandler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerBUserId,
                new RespondFinalOfferRequest(fixture.OfferBId, FinalOfferResponse.Accept, null)),
            CancellationToken.None);
        Assert.NotNull(resultB.ContractId);
        var contractBId = resultB.ContractId!.Value;
        Assert.NotEqual(contractAId, contractBId);

        Assert.Equal(2, fixture.Contracts.Entities.Count);
        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contracts.Entities.Single(c => c.ContractsId == contractAId).Status);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, fixture.Contracts.Entities.Single(c => c.ContractsId == contractBId).Status);

        // GetContractByJobPostQueryHandler must resolve to freelancer B's live contract, not
        // freelancer A's stale cancelled one, even though both rows exist for the same job
        // post (the core regression this test guards against).
        var jobPostHandler = new GetContractByJobPostQueryHandler(fixture.Context);
        var jobPostResult = await jobPostHandler.Handle(
            new GetContractByJobPostQuery(fixture.JobPostId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(contractBId, jobPostResult.ContractId);
        Assert.Equal(fixture.FreelancerBProfileId, jobPostResult.FreelancerProfileId);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, jobPostResult.Status);

        // Smoke test: the fresh contract has no EsignDocument row at all yet (Confirm hasn't
        // run), so both e-sign "get by contract" read handlers should still correctly 404 --
        // guarding against a future regression reintroducing the stale-document gap in this
        // adjacent scenario too.
        var getByContractHandler = new GetESignDocumentByContractQueryHandler(fixture.Context);
        await Assert.ThrowsAsync<NotFoundException>(
            () => getByContractHandler.Handle(
                new GetESignDocumentByContractQuery(contractBId, fixture.FreelancerBUserId),
                CancellationToken.None));

        var getStatusByContractHandler = new GetESignDocumentStatusByContractQueryHandler(fixture.Context);
        await Assert.ThrowsAsync<NotFoundException>(
            () => getStatusByContractHandler.Handle(
                new GetESignDocumentStatusByContractQuery(contractBId, fixture.FreelancerBUserId),
                CancellationToken.None));
    }

    private sealed class TwoFreelancerFixture
    {
        public TwoFreelancerFixture()
        {
            var clientUser = new User
            {
                UserId = ClientUserId,
                Role = (int)UserRole.Client,
                Email = "client@example.com",
                FullName = "Client User"
            };
            var freelancerAUser = new User
            {
                UserId = FreelancerAUserId,
                Role = (int)UserRole.Freelancer,
                Email = "freelancer-a@example.com",
                FullName = "Freelancer A"
            };
            var freelancerBUser = new User
            {
                UserId = FreelancerBUserId,
                Role = (int)UserRole.Freelancer,
                Email = "freelancer-b@example.com",
                FullName = "Freelancer B"
            };

            JobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Fixed job",
                Description = "Build it",
                Status = 1,
                CreatedAt = Now
            };
            var proposalA = new Proposal
            {
                ProposalsId = ProposalAId,
                JobPostsId = JobPostId,
                FreelancerProfilesId = FreelancerAProfileId,
                ProposedBudget = 1000m,
                Status = 0,
                JobPosts = JobPost
            };
            var proposalB = new Proposal
            {
                ProposalsId = ProposalBId,
                JobPostsId = JobPostId,
                FreelancerProfilesId = FreelancerBProfileId,
                ProposedBudget = 1500m,
                Status = 0,
                JobPosts = JobPost
            };
            var conversationA = new Conversation
            {
                ConversationsId = ConversationAId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = JobPostId,
                ProposalsId = ProposalAId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };
            var conversationB = new Conversation
            {
                ConversationsId = ConversationBId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = JobPostId,
                ProposalsId = ProposalBId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerAUser, freelancerBUser);
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(
                new FreelancerProfile { FreelancerProfilesId = FreelancerAProfileId, UserId = FreelancerAUserId },
                new FreelancerProfile { FreelancerProfilesId = FreelancerBProfileId, UserId = FreelancerBUserId });
            Context.AddSet(JobPost);
            Context.AddSet(proposalA, proposalB);
            Context.AddSet(conversationA, conversationB);
            Context.AddSet(
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationAId,
                    UserId = ClientUserId,
                    ParticipantRole = (int)ParticipantRole.Client,
                    JoinedAt = Now,
                    Conversations = conversationA
                },
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationAId,
                    UserId = FreelancerAUserId,
                    ParticipantRole = (int)ParticipantRole.Freelancer,
                    JoinedAt = Now,
                    Conversations = conversationA
                },
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationBId,
                    UserId = ClientUserId,
                    ParticipantRole = (int)ParticipantRole.Client,
                    JoinedAt = Now,
                    Conversations = conversationB
                },
                new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = ConversationBId,
                    UserId = FreelancerBUserId,
                    ParticipantRole = (int)ParticipantRole.Freelancer,
                    JoinedAt = Now,
                    Conversations = conversationB
                });
            Context.AddSet<Message>();
            Contracts = Context.AddSet<Contract>();
            Offers = Context.AddSet<NegotiationOffer>();
            Context.AddSet<NegotiationOfferMilestone>();
            Context.AddSet<Milestone>();
            Context.AddSet<ContractWorkItem>();
            Context.AddSet<ContractEscrow>();
            Context.AddSet<EsignDocument>();
            Context.AddSet<EsignDocumentContent>();
            Context.AddSet<EsignSignature>();
            Context.AddSet<DeliveryOutbox>();
            Context.AddSet<WalletTransaction>();
            Context.AddSet(
                new UserWallet
                {
                    UserWalletsId = Guid.NewGuid(),
                    UserId = FreelancerAUserId,
                    AvailableTokens = 10_000m,
                    CreatedAt = Now
                },
                new UserWallet
                {
                    UserWalletsId = Guid.NewGuid(),
                    UserId = FreelancerBUserId,
                    AvailableTokens = 10_000m,
                    CreatedAt = Now
                });
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerAUserId { get; } = Guid.NewGuid();
        public Guid FreelancerBUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerAProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerBProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ProposalAId { get; } = Guid.NewGuid();
        public Guid ProposalBId { get; } = Guid.NewGuid();
        public Guid ConversationAId { get; } = Guid.NewGuid();
        public Guid ConversationBId { get; } = Guid.NewGuid();
        public Guid OfferAId { get; } = Guid.NewGuid();
        public Guid OfferBId { get; } = Guid.NewGuid();
        public JobPost JobPost { get; }
        public TestDbSet<Contract> Contracts { get; }
        public TestDbSet<NegotiationOffer> Offers { get; }

        public void AddFinalOffer(
            Guid offerId,
            Guid conversationId,
            Guid proposalId,
            Guid freelancerProfileId,
            decimal finalPrice,
            params decimal[] amounts)
        {
            var offer = new NegotiationOffer
            {
                NegotiationOfferId = offerId,
                ConversationsId = conversationId,
                JobPostsId = JobPostId,
                ProposalsId = proposalId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = freelancerProfileId,
                FinalPrice = finalPrice,
                StartDate = DateOnly.FromDateTime(Now.AddDays(1)),
                EndDate = DateOnly.FromDateTime(Now.AddDays(30)),
                Status = (int)NegotiationOfferStatus.PendingFreelancerConfirmation,
                CreatedAt = Now
            };
            foreach (var (amount, index) in amounts.Select((amount, index) => (amount, index)))
            {
                var snapshot = new NegotiationOfferMilestone
                {
                    NegotiationOfferMilestoneId = Guid.NewGuid(),
                    NegotiationOfferId = offerId,
                    Title = $"Milestone {index + 1}",
                    Amount = amount,
                    Deliverables = $"Deliverable {index + 1}",
                    AcceptanceCriteria = $"Acceptance criteria {index + 1}",
                    OrderIndex = index
                };
                snapshot.WorkItems.Add(new NegotiationOfferWorkItem
                {
                    NegotiationOfferWorkItemId = Guid.NewGuid(),
                    NegotiationOfferMilestoneId = snapshot.NegotiationOfferMilestoneId,
                    Title = $"Work item {index + 1}",
                    Description = $"Complete milestone {index + 1} scope.",
                    Deliverables = $"Work item deliverable {index + 1}",
                    EstimatedDuration = "1 week",
                    OrderIndex = 0
                });
                offer.NegotiationOfferMilestones.Add(snapshot);
                Context.Set<NegotiationOfferMilestone>().Add(snapshot);
            }
            Offers.Add(offer);
        }

        public RespondFinalOfferCommandHandler CreateRespondFinalOfferHandler() =>
            new(Context, new FixedDateTimeService(Now), new NoopChatRealtimeNotifier());

        public CancelContractCommandHandler CreateCancelHandler() =>
            new(
                Context,
                // CancelContractCommandHandler enforces a 1-minute wait after
                // Contract.CreatedAt before self-service cancel is allowed.
                new FixedDateTimeService(Now.AddMinutes(2)),
                new NoopChatRealtimeNotifier(),
                new NoopNotificationService(),
                new CapturingUserAuditLogService(),
                NullLogger<CancelContractCommandHandler>.Instance);
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
