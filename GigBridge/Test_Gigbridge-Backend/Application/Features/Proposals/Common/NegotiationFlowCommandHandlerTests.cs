using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Chat.Common.FinalOffers.Create.Commands;
using Application.Features.Chat.Common.FinalOffers.Create.DTOs;
using Application.Features.Chat.Common.FinalOffers.Respond.Commands;
using Application.Features.Chat.Common.FinalOffers.Respond.DTOs;
using Application.Features.Chat.Common.FinalOffers.Shared.Email;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.Commands;
using Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;
using Application.Features.Chat.Common.Negotiations.MilestonePlans.DTOs;
using Application.Features.Proposals.Common.AcceptForNegotiation.Commands;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class NegotiationFlowCommandHandlerTests
{
    [Fact]
    public async Task StartNegotiationFromProposal_ForwardsToCanonicalProposalWorkflow()
    {
        var sender = Substitute.For<ISender>();
        var proposalId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expectedConversationId = Guid.NewGuid();
        sender.Send(Arg.Any<AcceptProposalForNegotiationCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedConversationId);
        var handler = new StartNegotiationFromProposalCommandHandler(sender);

        var conversationId = await handler.Handle(
            new StartNegotiationFromProposalCommand(proposalId, userId),
            CancellationToken.None);

        Assert.Equal(expectedConversationId, conversationId);
        await sender.Received(1).Send(
            Arg.Is<AcceptProposalForNegotiationCommand>(command => command.ProposalId == proposalId && command.UserId == userId),
            Arg.Any<CancellationToken>());
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
    public async Task CreateFinalOffer_RejectsWhenJobPostIsClosed()
    {
        var fixture = new NegotiationFixture();
        fixture.JobPost.Status = 2;
        fixture.AddConversationWithParticipants();
        var handler = new CreateFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new CreateFinalOfferCommand(
                    fixture.ClientUserId,
                    new CreateFinalOfferRequest(
                        fixture.ConversationId,
                        1500m,
                        "Build the first production release.",
                        null,
                        null,
                        null,
                        CreatePlan(600m, 900m))),
                CancellationToken.None));

        Assert.Contains("no longer open", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task UpdateNegotiationMilestonePlan_RejectsWhenJobPostIsClosed()
    {
        var fixture = new NegotiationFixture();
        fixture.JobPost.Status = 2;
        fixture.AddConversationWithParticipants();
        var handler = new UpdateNegotiationMilestonePlanCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateNegotiationMilestonePlanCommand(
                    fixture.ConversationId,
                    fixture.ClientUserId,
                    new UpdateNegotiationMilestonePlanRequest(CreatePlan(600m, 900m))),
                CancellationToken.None));

        Assert.Contains("no longer open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.NegotiationDrafts.Entities);
    }

    [Fact]
    public async Task RespondFinalOffer_AcceptCreatesContractForPlanConfirmationWithoutEscrow()
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

        var notifications = Substitute.For<INotificationService>();
        var emailService = Substitute.For<IEmailService>();
        var emailRenderer = Substitute.For<IJobAcceptanceEmailRenderer>();
        emailRenderer.Render(Arg.Any<JobAcceptanceEmailModel>()).Returns(
            new RenderedJobAcceptanceEmail("Accepted", "<p>Accepted</p>", "Accepted"));
        var configuration = Substitute.For<IConfiguration>();
        configuration["FrontendBaseUrl"].Returns("https://gigbridge.test");

        var handler = new RespondFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            notifications,
            emailService,
            emailRenderer,
            configuration,
            Substitute.For<ILogger<RespondFinalOfferCommandHandler>>());

        var result = await handler.Handle(
            new RespondFinalOfferCommand(
                fixture.FreelancerUserId,
                new RespondFinalOfferRequest(
                    fixture.OfferId,
                    FinalOfferResponse.Accept,
                    null)),
            CancellationToken.None);

        Assert.Equal((int)NegotiationOfferStatus.Accepted, fixture.Offers.Entities[0].Status);
        var contract = Assert.Single(fixture.Contracts.Entities);
        Assert.Equal(fixture.FreelancerProfileId, contract.FreelancerProfilesId);
        Assert.Equal(fixture.ProposalId, contract.ProposalsId);
        Assert.Equal(1500m, contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, contract.Status);
        Assert.Equal(3, fixture.Proposal.Status);
        Assert.Equal(1, waitlistedProposal.Status);
        Assert.Equal(contract.ContractsId, result.ContractId);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, result.ContractStatus);
        Assert.Empty(fixture.Escrows.Entities);
        await notifications.Received(1).CreateNotificationAsync(
            fixture.FreelancerUserId,
            NotificationType.ContractStarted,
            Arg.Is<string>(title => title.Contains("Fixed job")),
            Arg.Any<string>(),
            fixture.ContractId,
            "Contract",
            Arg.Any<CancellationToken>());
        await emailService.Received(1).SendEmailAsync(
            Arg.Is<EmailRequest>(email =>
                email.To == "freelancer@example.com" &&
                email.Subject == "Accepted" &&
                email.IsHtml),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RespondFinalOffer_AcceptRejectsWhenJobPostIsClosed()
    {
        var fixture = new NegotiationFixture();
        fixture.JobPost.Status = 2;
        fixture.AddConversationWithParticipants();
        fixture.AddOfferWithSnapshot(1500m, 600m, 900m);

        var handler = new RespondFinalOfferCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new RespondFinalOfferCommand(
                    fixture.FreelancerUserId,
                    new RespondFinalOfferRequest(
                        fixture.OfferId,
                        FinalOfferResponse.Accept,
                        null)),
                CancellationToken.None));

        Assert.Contains("no longer open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((int)NegotiationOfferStatus.PendingFreelancerConfirmation, fixture.Offers.Entities[0].Status);
        Assert.Empty(fixture.Contracts.Entities);
        Assert.Empty(fixture.Escrows.Entities);
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
        var contract = Assert.Single(fixture.Contracts.Entities);
        Assert.Equal(1200m, contract.TotalBudget);
        Assert.Equal((int)ContractStatus.PendingContractConfirmation, result.ContractStatus);
        Assert.Equal(1200m, fixture.Milestones.Entities.Sum(milestone => milestone.Amount));
        Assert.All(fixture.Milestones.Entities, milestone => Assert.Equal(600m, milestone.Amount));
        Assert.All(fixture.Milestones.Entities, milestone => Assert.False(string.IsNullOrWhiteSpace(milestone.AcceptanceCriteria)));
        Assert.Equal(2, fixture.Milestones.Entities.Sum(milestone => milestone.WorkItems.Count));
        Assert.Empty(fixture.Escrows.Entities);
    }

    private sealed class NegotiationFixture
    {
        public NegotiationFixture(bool includeContract = false)
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
            Context.AddSet(new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = FreelancerUserId,
                AvailableTokens = 100m,
                HeldTokens = 0m,
                CreatedAt = Now
            });
            Context.AddSet<WalletTransaction>();
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
                ContractsId = null,
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
                ContractsId = null,
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
            OrderIndex = index,
            WorkItems = [new NegotiationWorkItemDto
            {
                Title = $"Work item {index + 1}",
                Description = $"Complete milestone {index + 1} scope.",
                Deliverables = $"Work item deliverable {index + 1}",
                EstimatedDuration = "1 week",
                OrderIndex = 0
            }]
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
