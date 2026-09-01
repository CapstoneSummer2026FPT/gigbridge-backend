using Application.Common.Exceptions;
using Application.Common.Interfaces.Caching;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Auditing.Interfaces;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Contracts.Cancellation.Common.Cancel.Commands;
using Application.Features.Contracts.Completion.Client.Commands;
using Application.Features.Contracts.Details.Freelancer.Confirm.Commands;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Application.Features.Contracts.Signing.Common.Sign.Commands;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Application.Features.ESign.Common.GetDocumentByContract.Queries;
using Application.Features.ESign.Common.GetDocumentStatusByContract.Queries;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;
using Domain.Enums.ESign;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

/// <summary>
/// Regression coverage for the 23505 duplicate-key bug: a Contract's ProposalsId has a
/// strict, unfiltered unique index, so renegotiating and re-accepting a final offer for the
/// same proposal after its earlier Contract was cancelled must reuse that Contract row
/// instead of inserting a second one. This drives the full real-handler lifecycle from that
/// reused row through to a completed contract, proving the reset state behaves identically
/// to a freshly created contract at every step.
/// </summary>
public class RespondFinalOfferReuseAfterCancellationTests
{
    private const string SignatureDataUri = "data:image/png;base64,aGVsbG8=";

    [Fact]
    public async Task CancelledContract_RenegotiatedAndAcceptedAgain_ReachesCompleted()
    {
        var fixture = new ReuseFixture();

        // Step 1: first negotiation is accepted -> a Contract row is created.
        fixture.AddFinalOffer(fixture.FirstOfferId, 1000m, 1000m);
        var acceptHandler = fixture.CreateRespondFinalOfferHandler();
        var firstResult = await acceptHandler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerUserId,
                new RespondFinalOfferRequest(fixture.FirstOfferId, FinalOfferResponse.Accept, null)),
            CancellationToken.None);

        Assert.NotNull(firstResult.ContractId);
        var originalContractId = firstResult.ContractId!.Value;
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, firstResult.ContractStatus);
        Assert.Single(fixture.Contracts.Entities);

        // Step 1.5: the freelancer confirms contract details, creating the first
        // EsignDocument, before the client cancels — this reproduces the real scenario the
        // reuse fix must handle: an abandoned attempt that already has a document to void,
        // not just a bare pre-confirmation contract.
        var confirmHandlerForFirstAttempt = fixture.CreateConfirmHandler();
        await confirmHandlerForFirstAttempt.Handle(
            new ConfirmContractDetailsCommand(originalContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contracts.Entities.Single().Status);
        Assert.Single(fixture.Context.Set<EsignDocument>());

        // Step 2: the client cancels before signing completes.
        var cancelHandler = fixture.CreateCancelHandler();
        await cancelHandler.Handle(
            new CancelContractCommand(originalContractId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Cancelled, fixture.Contracts.Entities.Single().Status);

        // Step 3: the client and freelancer renegotiate from the same proposal with a
        // different price and milestone plan, and the freelancer accepts again. Before the
        // fix, this second Accept would throw 23505 on Contracts_propo_ProposalsId_key.
        var secondOfferId = Guid.NewGuid();
        fixture.AddFinalOffer(secondOfferId, 1200m, 1200m);
        var secondResult = await acceptHandler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerUserId,
                new RespondFinalOfferRequest(secondOfferId, FinalOfferResponse.Accept, null)),
            CancellationToken.None);

        var contract = Assert.Single(fixture.Contracts.Entities);
        Assert.Equal(originalContractId, contract.ContractsId);
        Assert.Equal(originalContractId, secondResult.ContractId);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, contract.Status);
        Assert.Equal(1200m, contract.TotalBudget);
        Assert.Null(contract.CancelledAt);
        Assert.Null(contract.CancelledByUserId);

        // Step 3.5: before Confirm runs, the only EsignDocument row for this contract is the
        // Voided one from the abandoned attempt. Both e-sign "get by contract" read handlers
        // (what the frontend's sign page loads) must treat that as "no document yet" (404)
        // rather than returning the stale Voided document as if it were current — otherwise
        // the sign page would render a broken/incorrect signing UI instead of the normal
        // "document not created yet" state.
        Assert.Single(fixture.Context.Set<EsignDocument>());
        Assert.Equal(
            (int)ESignDocumentStatus.Voided,
            fixture.Context.Set<EsignDocument>().Single().Status);

        var getByContractHandler = new GetESignDocumentByContractQueryHandler(fixture.Context);
        await Assert.ThrowsAsync<NotFoundException>(
            () => getByContractHandler.Handle(
                new GetESignDocumentByContractQuery(originalContractId, fixture.FreelancerUserId),
                CancellationToken.None));

        var getStatusByContractHandler = new GetESignDocumentStatusByContractQueryHandler(fixture.Context);
        await Assert.ThrowsAsync<NotFoundException>(
            () => getStatusByContractHandler.Handle(
                new GetESignDocumentStatusByContractQuery(originalContractId, fixture.FreelancerUserId),
                CancellationToken.None));

        // Step 4: the freelancer confirms contract details -> a fresh escrow and e-sign
        // document are created for the reused contract.
        var confirmHandler = fixture.CreateConfirmHandler();
        await confirmHandler.Handle(
            new ConfirmContractDetailsCommand(originalContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.PendingSignature, contract.Status);

        var document = Assert.Single(
            fixture.Context.Set<EsignDocument>(),
            d => d.Status != (int)ESignDocumentStatus.Voided);
        var escrow = Assert.Single(fixture.Context.Set<ContractEscrow>());
        Assert.Equal(1200m, escrow.RequiredAmount);
        Assert.Equal(0m, escrow.FundedAmount);

        // The fresh document is now correctly discoverable via both read handlers.
        var liveDocument = await getByContractHandler.Handle(
            new GetESignDocumentByContractQuery(originalContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal(document.EsignDocumentsId, liveDocument.DocumentId);
        Assert.NotEqual((int)ESignDocumentStatus.Voided, liveDocument.Status);

        var liveStatus = await getStatusByContractHandler.Handle(
            new GetESignDocumentStatusByContractQuery(originalContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        Assert.Equal(document.EsignDocumentsId, liveStatus.DocumentId);
        Assert.NotEqual((int)ESignDocumentStatus.Voided, liveStatus.Status);

        // Step 5: both parties sign (identities are pre-seeded on the fixture's users, so
        // signing does not need the identity-verification cache dependency).
        var signHandler = fixture.CreateSignHandler();
        await signHandler.Handle(
            new SignContractCommand(
                originalContractId,
                fixture.ClientUserId,
                new SignContractRequest(SignatureDataUri, 300, 100, fixture.ClientIdentityCode, true, "Ver 1.0 Gigbridge"),
                null,
                null),
            CancellationToken.None);
        await signHandler.Handle(
            new SignContractCommand(
                originalContractId,
                fixture.FreelancerUserId,
                new SignContractRequest(SignatureDataUri, 300, 100, fixture.FreelancerIdentityCode, true, "Ver 1.0 Gigbridge"),
                null,
                null),
            CancellationToken.None);

        Assert.Equal((int)ESignDocumentStatus.FullySigned, document.Status);
        Assert.Equal((int)ContractStatus.PendingEscrow, contract.Status);

        // Step 6: the client funds escrow -> the contract goes Active.
        var fundHandler = fixture.CreateFundHandler();
        var fundResult = await fundHandler.Handle(
            new FundContractEscrowCommand(originalContractId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Active, fundResult.ContractStatus);
        Assert.Equal((int)ContractStatus.Active, contract.Status);

        // Step 7: the single milestone is approved and the project is ended -> Completed,
        // proving the reused contract completes exactly like a freshly created one.
        var milestone = Assert.Single(fixture.Context.Set<Milestone>());
        milestone.Status = (int)MilestoneStatus.Approved;
        milestone.SubmittedAt = fixture.Now;
        milestone.ApprovedAt = fixture.Now;

        var endHandler = fixture.CreateEndProjectHandler();
        var endResult = await endHandler.Handle(
            new EndProjectCommand(originalContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Completed, endResult.ContractStatus);
        Assert.Equal((int)ContractStatus.Completed, contract.Status);
        Assert.NotNull(contract.CompletedAt);
    }

    private sealed class ReuseFixture
    {
        public ReuseFixture()
        {
            var clientUser = new User
            {
                UserId = ClientUserId,
                Role = (int)UserRole.Client,
                Email = "client@example.com",
                FullName = "Client User",
                IdentityOrTaxCode = ClientIdentityCode
            };
            var freelancerUser = new User
            {
                UserId = FreelancerUserId,
                Role = (int)UserRole.Freelancer,
                Email = "freelancer@example.com",
                FullName = "Freelancer User",
                IdentityOrTaxCode = FreelancerIdentityCode
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
            Proposal = new Proposal
            {
                ProposalsId = ProposalId,
                JobPostsId = JobPostId,
                FreelancerProfilesId = FreelancerProfileId,
                ProposedBudget = 1000m,
                Status = 0,
                JobPosts = JobPost
            };
            Conversation = new Conversation
            {
                ConversationsId = ConversationId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = JobPostId,
                ProposalsId = ProposalId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerUser);
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(JobPost);
            Context.AddSet(Proposal);
            Context.AddSet(Conversation);
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
            Context.AddSet<EscrowTransaction>();
            Context.AddSet(new EsignTemplate
            {
                EsignTemplatesId = Guid.NewGuid(),
                Name = "Fixed price contract",
                TemplateCode = "CONTRACT_FIXED_PRICE",
                HtmlContent = "<html>{{Contract.Title}}<table>{{MilestonesHtml}}</table></html>",
                Version = 1,
                IsActive = true,
                CreatedAt = Now
            });
            Context.AddSet(
                new UserWallet
                {
                    UserWalletsId = Guid.NewGuid(),
                    UserId = ClientUserId,
                    AvailableTokens = 10_000m,
                    CreatedAt = Now
                },
                new UserWallet
                {
                    UserWalletsId = Guid.NewGuid(),
                    UserId = FreelancerUserId,
                    AvailableTokens = 10_000m,
                    CreatedAt = Now
                });
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ProposalId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Guid FirstOfferId { get; } = Guid.NewGuid();
        public string ClientIdentityCode { get; } = "012345678901";
        public string FreelancerIdentityCode { get; } = "109876543210";
        public JobPost JobPost { get; }
        public Proposal Proposal { get; }
        public Conversation Conversation { get; }
        public TestDbSet<Contract> Contracts { get; }
        public TestDbSet<NegotiationOffer> Offers { get; }

        public void AddFinalOffer(Guid offerId, decimal finalPrice, params decimal[] amounts)
        {
            var offer = new NegotiationOffer
            {
                NegotiationOfferId = offerId,
                ConversationsId = ConversationId,
                JobPostsId = JobPostId,
                ProposalsId = ProposalId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
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

        public ConfirmContractDetailsCommandHandler CreateConfirmHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopChatRealtimeNotifier(),
                new FakeContractEsignDocumentGenerator(),
                new CapturingUserAuditLogService());

        public SignContractCommandHandler CreateSignHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopChatRealtimeNotifier(),
                new FakeMediaService(),
                new FakeContractEsignDocumentGenerator(),
                new FakeWordToPdfConverter(),
                new CapturingUserAuditLogService(),
                Substitute.For<ICacheService>(),
                NullLogger<SignContractCommandHandler>.Instance);

        public FundContractEscrowCommandHandler CreateFundHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopNotificationService(),
                new NoopChatRealtimeNotifier(),
                new CapturingUserAuditLogService());

        public EndProjectCommandHandler CreateEndProjectHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopChatRealtimeNotifier(),
                new NoopNotificationService());
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
