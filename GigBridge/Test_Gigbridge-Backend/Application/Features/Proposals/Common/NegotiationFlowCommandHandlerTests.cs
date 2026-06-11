using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.FinalOffers.Create.Commands;
using Application.Features.Chat.Common.FinalOffers.Create.DTOs;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;
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
            new FixedDateTimeService(fixture.Now));

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
            new FixedDateTimeService(fixture.Now));

        var conversationId = await handler.Handle(
            new StartNegotiationFromProposalCommand(fixture.ProposalId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(existingConversationId, conversationId);
        Assert.Single(fixture.Conversations.Entities);
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
                    "Please confirm the final scope.")),
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
    public async Task RespondFinalOffer_AcceptUpdatesDraftContractButDoesNotCreateEscrow()
    {
        var fixture = new NegotiationFixture();
        fixture.AddConversationWithParticipants();
        fixture.Offers.Add(new NegotiationOffer
        {
            NegotiationOfferId = fixture.OfferId,
            ConversationsId = fixture.ConversationId,
            JobPostsId = fixture.JobPostId,
            ContractsId = fixture.ContractId,
            ProposalsId = fixture.ProposalId,
            ClientProfilesId = fixture.ClientProfileId,
            FreelancerProfilesId = fixture.FreelancerProfileId,
            FinalPrice = 1500m,
            StartDate = DateOnly.FromDateTime(fixture.Now.AddDays(1)),
            EndDate = DateOnly.FromDateTime(fixture.Now.AddDays(30)),
            Status = (int)NegotiationOfferStatus.PendingFreelancerConfirmation,
            CreatedAt = fixture.Now
        });

        var handler = new RespondFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        await handler.Handle(
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
        Assert.Equal((int)ContractStatus.PendingContractDetails, fixture.Contract.Status);
        Assert.Equal(2, fixture.Proposal.Status);
        Assert.Empty(fixture.Escrows.Entities);
    }

    private sealed class NegotiationFixture
    {
        public NegotiationFixture()
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
            Context.AddSet(Contract);
            Conversations = Context.AddSet<Conversation>();
            Participants = Context.AddSet<ConversationParticipant>();
            Messages = Context.AddSet<Message>();
            Offers = Context.AddSet<NegotiationOffer>();
            Escrows = Context.AddSet<ContractEscrow>();
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
        public TestDbSet<Conversation> Conversations { get; }
        public TestDbSet<ConversationParticipant> Participants { get; }
        public TestDbSet<Message> Messages { get; }
        public TestDbSet<NegotiationOffer> Offers { get; }
        public TestDbSet<ContractEscrow> Escrows { get; }
        public JobPost JobPost { get; }
        public Proposal Proposal { get; }
        public Contract Contract { get; }

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

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
