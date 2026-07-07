using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.FinalOffers.Create.Commands;
using Application.Features.Chat.Common.FinalOffers.Create.DTOs;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class NegotiationFlowCommandHandlerTests
{
    [Fact]
    public async Task StartNegotiationFromProposal_CreatesJobNegotiationConversationWithParticipants()
    {
        var fixture = new NegotiationFixture();
        var handler = new StartNegotiationFromProposalCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var conversationId = await handler.Handle(
            new StartNegotiationFromProposalCommand(fixture.ProposalId, fixture.ClientUserId),
            CancellationToken.None);

        var conversation = Assert.Single(fixture.Conversations.Entities);
        Assert.Equal(conversation.ConversationsId, conversationId);
        Assert.Equal((int)ConversationType.JobNegotiation, conversation.ConversationType);
        Assert.Equal(fixture.JobPostId, conversation.JobPostsId);
        Assert.Equal(fixture.ProposalId, conversation.ProposalsId);
        Assert.Equal(fixture.ContractId, conversation.ContractsId);

        Assert.Equal((int)ContractStatus.InNegotiation, fixture.Contract.Status);
        Assert.Contains(fixture.Participants.Entities, participant =>
            participant.ConversationsId == conversationId &&
            participant.UserId == fixture.ClientUserId &&
            participant.ParticipantRole == (int)ParticipantRole.Client);
        Assert.Contains(fixture.Participants.Entities, participant =>
            participant.ConversationsId == conversationId &&
            participant.UserId == fixture.FreelancerUserId &&
            participant.ParticipantRole == (int)ParticipantRole.Freelancer);
    }

    [Fact]
    public async Task StartNegotiationFromProposal_ReturnsExistingConversationForSameProposal()
    {
        var fixture = new NegotiationFixture();
        var existingConversationId = Guid.NewGuid();
        fixture.Conversations.Add(new Conversation
        {
            ConversationsId = existingConversationId,
            ConversationType = (int)ConversationType.JobNegotiation,
            JobPostsId = fixture.JobPostId,
            ProposalsId = fixture.ProposalId,
            ContractsId = fixture.ContractId,
            CreatedByUserId = fixture.ClientUserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = fixture.Now
        });

        var handler = new StartNegotiationFromProposalCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var conversationId = await handler.Handle(
            new StartNegotiationFromProposalCommand(fixture.ProposalId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(existingConversationId, conversationId);
        Assert.Single(fixture.Conversations.Entities);
    }

    [Fact]
    public async Task StartNegotiationFromProposal_CreatesDraftContractWhenJobPostHasNoContract()
    {
        var fixture = new NegotiationFixture(includeContract: false);
        fixture.JobPost.BudgetMin = null;
        fixture.JobPost.BudgetMax = 1800m;
        var handler = new StartNegotiationFromProposalCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var conversationId = await handler.Handle(
            new StartNegotiationFromProposalCommand(fixture.ProposalId, fixture.ClientUserId),
            CancellationToken.None);

        var contract = Assert.Single(fixture.Contracts.Entities);
        Assert.Equal(fixture.JobPostId, contract.JobPostsId);
        Assert.Equal(fixture.ClientProfileId, contract.ClientProfilesId);
        Assert.Equal(fixture.JobPost.Title, contract.Title);
        Assert.Equal(fixture.JobPost.Description, contract.Description);
        Assert.Equal(1800m, contract.TotalBudget);
        Assert.Equal((int)ContractStatus.InNegotiation, contract.Status);

        var conversation = Assert.Single(fixture.Conversations.Entities);
        Assert.Equal(conversation.ConversationsId, conversationId);
        Assert.Equal(contract.ContractsId, conversation.ContractsId);
    }

    [Fact]
    public async Task CreateFinalOffer_ClientParticipantCreatesOfferAndFinalOfferMessage()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        var handler = new CreateFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var offerId = await handler.Handle(
            new CreateFinalOfferCommand(
                fixture.ClientUserId,
                new CreateFinalOfferRequest(
                    fixture.ConversationId,
                    1500m,
                    "Build the first production release.",
                    DateOnly.FromDateTime(fixture.Now.AddDays(1)),
                    DateOnly.FromDateTime(fixture.Now.AddDays(30)),
                    "Please confirm the final scope.",
                    CreatePlan(600m, 900m))),
            CancellationToken.None);

        var offer = Assert.Single(fixture.Offers.Entities);
        Assert.Equal(offer.NegotiationOfferId, offerId);
        Assert.Equal((int)NegotiationOfferStatus.PendingFreelancerConfirmation, offer.Status);
        Assert.Equal(1500m, offer.FinalPrice);

        var message = Assert.Single(fixture.Messages.Entities);
        Assert.Equal((int)MessageType.FinalOffer, message.MessageType);
        Assert.Equal(fixture.ClientUserId, message.SenderUserId);
        Assert.Contains(offerId.ToString(), message.Metadata);
    }

    [Fact]
    public async Task CreateFinalOffer_RejectsWhenMilestonesAreMissing()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        var handler = new CreateFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CreateFinalOfferCommand(
                    fixture.ClientUserId,
                    new CreateFinalOfferRequest(
                        fixture.ConversationId,
                        1500m,
                        "Build the first production release.",
                        null,
                        null,
                        null)),
                CancellationToken.None));

        Assert.Equal("At least one milestone is required for a final offer.", ex.Message);
        Assert.Empty(fixture.Offers.Entities);
        Assert.Empty(fixture.Messages.Entities);
    }

    [Fact]
    public async Task CreateFinalOffer_RejectsFinalPriceDifferentFromMilestoneTotal()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        var handler = new CreateFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new CreateFinalOfferCommand(
                fixture.ClientUserId,
                new CreateFinalOfferRequest(
                    fixture.ConversationId,
                    1500m,
                    "Build the first production release.",
                    null,
                    null,
                    null,
                    CreatePlan(500m, 500m))),
            CancellationToken.None));

        Assert.Contains("must equal", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Offers.Entities);
    }

    [Fact]
    public async Task CreateFinalOffer_FreelancerParticipantCannotCreateOffer()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        var handler = new CreateFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new CreateFinalOfferCommand(
                    fixture.FreelancerUserId,
                    new CreateFinalOfferRequest(
                        fixture.ConversationId,
                        1500m,
                        "Build the first production release.",
                        null,
                        null,
                        null)),
                CancellationToken.None));
    }

    [Fact]
    public async Task RespondFinalOffer_AcceptMovesContractToSignatureAndCreatesEscrow()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        var waitlistedProposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = fixture.JobPostId,
            FreelancerProfilesId = Guid.NewGuid(),
            ProposedBudget = 1400m,
            Status = 1,
            JobPosts = fixture.JobPost
        };
        fixture.Context.Set<Proposal>().Add(waitlistedProposal);
        fixture.AddOfferWithSnapshot(1500m, 600m, 900m);

        var handler = new RespondFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var result = await handler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerUserId,
                new RespondFinalOfferRequest(
                    fixture.OfferId,
                    FinalOfferResponse.Accept,
                    null)),
            CancellationToken.None);

        Assert.Equal((int)NegotiationOfferStatus.Accepted, fixture.Offers.Entities[0].Status);
        Assert.Equal(fixture.FreelancerProfileId, fixture.Contract.FreelancerProfilesId);
        Assert.Equal(fixture.ProposalId, fixture.Contract.ProposalsId);
        Assert.Equal(1500m, fixture.Contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal(3, fixture.Proposal.Status);
        Assert.Equal(1, waitlistedProposal.Status);
        Assert.Equal(fixture.ContractId, result.ContractId);
        Assert.Equal((int)ContractStatus.PendingSignature, result.ContractStatus);
        var escrow = Assert.Single(fixture.Escrows.Entities);
        Assert.Equal(1500m, escrow.RequiredAmount);
        Assert.Equal(0m, escrow.FundedAmount);
        Assert.Equal(1.0m, escrow.RequiredPercentage);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, escrow.Status);
    }

    [Fact]
    public async Task RespondFinalOffer_AcceptMaterializesImmutableSnapshot()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        fixture.AddOfferWithSnapshot(1200m, 600m, 600m);

        var handler = new RespondFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var result = await handler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerUserId,
                new RespondFinalOfferRequest(
                    fixture.OfferId,
                    FinalOfferResponse.Accept,
                    null)),
            CancellationToken.None);

        Assert.Equal((int)NegotiationOfferStatus.Accepted, fixture.Offers.Entities[0].Status);
        Assert.Equal(1200m, fixture.Contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingSignature, result.ContractStatus);
        Assert.Equal(1200m, fixture.Milestones.Entities.Sum(milestone => milestone.Amount));
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(600m, milestone.Amount));
        Assert.All(fixture.Milestones.Entities, milestone => Assert.False(string.IsNullOrWhiteSpace(milestone.AcceptanceCriteria)));
        var escrow = Assert.Single(fixture.Escrows.Entities);
        Assert.Equal(1200m, escrow.RequiredAmount);
    }

    private sealed class NegotiationFixture
    {
        public NegotiationFixture(bool includeContract = true)
        {
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
                ProposedBudget = 1200m,
                Status = 0,
                JobPosts = JobPost
            };
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Fixed job",
                TotalBudget = 1000m,
                Status = (int)ContractStatus.PendingFreelancerSelection,
                CreatedAt = Now
            };

            Context.AddSet(
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(JobPost);
            Context.AddSet(Proposal);
            Contracts = includeContract
                ? Context.AddSet(Contract)
                : Context.AddSet<Contract>();
            Conversations = Context.AddSet<Conversation>();
            Participants = Context.AddSet<ConversationParticipant>();
            Messages = Context.AddSet<Message>();
            Offers = Context.AddSet<NegotiationOffer>();
            Escrows = Context.AddSet<ContractEscrow>();
            Milestones = Context.AddSet<Milestone>();
            OfferMilestones = Context.AddSet<NegotiationOfferMilestone>();
            NegotiationDrafts = Context.AddSet<NegotiationMilestoneDraft>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ProposalId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Guid OfferId { get; } = Guid.NewGuid();
        public TestDbSet<Contract> Contracts { get; }
        public TestDbSet<Conversation> Conversations { get; }
        public TestDbSet<ConversationParticipant> Participants { get; }
        public TestDbSet<Message> Messages { get; }
        public TestDbSet<NegotiationOffer> Offers { get; }
        public TestDbSet<ContractEscrow> Escrows { get; }
        public TestDbSet<Milestone> Milestones { get; }
        public TestDbSet<NegotiationOfferMilestone> OfferMilestones { get; }
        public TestDbSet<NegotiationMilestoneDraft> NegotiationDrafts { get; }
        public JobPost JobPost { get; }
        public Proposal Proposal { get; }
        public Contract Contract { get; }

        public void AddMilestones(decimal firstAmount, decimal secondAmount)
        {
            Milestones.Add(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = firstAmount,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            });
            Milestones.Add(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = secondAmount,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            });
        }

        public void AddOfferWithSnapshot(decimal finalPrice, params decimal[] amounts)
        {
            var offer = new NegotiationOffer
            {
                NegotiationOfferId = OfferId,
                ConversationsId = ConversationId,
                JobPostsId = JobPostId,
                ContractsId = ContractId,
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
                    NegotiationOfferId = OfferId,
                    Title = $"Milestone {index + 1}",
                    Amount = amount,
                    Deliverables = $"Deliverable {index + 1}",
                    AcceptanceCriteria = $"Acceptance criteria {index + 1}",
                    OrderIndex = index
                };
                offer.NegotiationOfferMilestones.Add(snapshot);
                OfferMilestones.Add(snapshot);
            }
            Offers.Add(offer);
        }

        public void AddConversationWithParticipants()
        {
            Conversations.Add(new Conversation
            {
                ConversationsId = ConversationId,
                ConversationType = (int)ConversationType.JobNegotiation,
                JobPostsId = JobPostId,
                ProposalsId = ProposalId,
                ContractsId = ContractId,
                CreatedByUserId = ClientUserId,
                Status = (int)ConversationStatus.Active,
                CreatedAt = Now
            });
            Participants.Add(new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = ConversationId,
                UserId = ClientUserId,
                ParticipantRole = (int)ParticipantRole.Client,
                JoinedAt = Now
            });
            Participants.Add(new ConversationParticipant
            {
                ConversationParticipantId = Guid.NewGuid(),
                ConversationsId = ConversationId,
                UserId = FreelancerUserId,
                ParticipantRole = (int)ParticipantRole.Freelancer,
                JoinedAt = Now
            });
        }
    }

    private static IReadOnlyCollection<NegotiationMilestoneDto> CreatePlan(params decimal[] amounts)
    {
        return amounts.Select((amount, index) => new NegotiationMilestoneDto
        {
            Title = $"Milestone {index + 1}",
            Amount = amount,
            Deliverables = $"Deliverable {index + 1}",
            AcceptanceCriteria = $"Acceptance criteria {index + 1}",
            OrderIndex = index
        }).ToList();
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
