using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Conversations.EnsureParticipant.Queries;
using Application.Features.Proposals.Common.AcceptForNegotiation.Commands;
using Application.Features.Proposals.Common.Email;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;
using Xunit;

namespace Test_Gigbridge_Backend.Application.Features.Proposals.Common;

public class AcceptProposalForNegotiationCommandHandlerTests
{
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IProposalNegotiationEmailRenderer _emailRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AcceptProposalForNegotiationCommandHandler> _logger;

    public AcceptProposalForNegotiationCommandHandlerTests()
    {
        _dateTimeService = new FixedDateTimeService(new DateTime(2026, 6, 11, 8, 0, 0, DateTimeKind.Utc));
        _chatRealtimeNotifier = Substitute.For<IChatRealtimeNotifier>();
        _notificationService = Substitute.For<INotificationService>();
        _emailService = Substitute.For<IEmailService>();
        _emailRenderer = Substitute.For<IProposalNegotiationEmailRenderer>();
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<AcceptProposalForNegotiationCommandHandler>>();

        _configuration["FrontendBaseUrl"].Returns("http://localhost:5173");
        _emailRenderer.Render(Arg.Any<ProposalNegotiationEmailModel>())
            .Returns(new RenderedProposalNegotiationEmail("Subject", "HtmlBody", "TextBody"));
    }

    [Fact]
    public async Task Handle_FirstTimeAccept_CreatesConversationAndSendsNotifications()
    {
        // Arrange
        var fixture = new TestFixture();
        var handler = CreateHandler(fixture);

        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, fixture.ClientUserId);

        // Act
        var conversationId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, conversationId);
        
        // Assert proposal updated to Shortlisted (2)
        Assert.Equal(2, fixture.Proposal.Status);

        // Assert conversation created with correct details
        var conversation = Assert.Single(fixture.Conversations.Entities);
        Assert.Equal(conversationId, conversation.ConversationsId);
        Assert.Equal((int)ConversationType.JobNegotiation, conversation.ConversationType);
        Assert.Equal(fixture.JobPostId, conversation.JobPostsId);
        Assert.Equal(fixture.ProposalId, conversation.ProposalsId);

        // Assert participants added
        Assert.Contains(fixture.Participants.Entities, p => p.UserId == fixture.ClientUserId && p.ParticipantRole == (int)ParticipantRole.Client);
        Assert.Contains(fixture.Participants.Entities, p => p.UserId == fixture.FreelancerUserId && p.ParticipantRole == (int)ParticipantRole.Freelancer);

        // Proposal payment plan seeds this conversation's draft, not executable contract milestones.
        Assert.Empty(fixture.Milestones.Entities);
        Assert.Equal(2, fixture.NegotiationDrafts.Entities.Count);
        Assert.Equal(1500m, fixture.NegotiationDrafts.Entities.Sum(milestone => milestone.Amount));
        Assert.Empty(fixture.EscrowTransactions.Entities);

        // Assert realtime notification called
        await _notificationService.Received(1).CreateNotificationAsync(
            fixture.FreelancerUserId,
            NotificationType.ProposalStatusChanged,
            "Proposal accepted for negotiation",
            Arg.Any<string>(),
            fixture.ProposalId,
            "Proposal",
            Arg.Any<CancellationToken>());

        // Assert email sent
        //await _emailService.Received(1).SendEmailAsync(
        //    Arg.Is<Application.Features.Auth.Shared.DTOs.EmailRequest>(e => 
        //        e.To == "freelancer@example.com" &&
        //        e.Subject == "Subject" &&
        //        e.Body == "HtmlBody"),
        //    Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReopenReusedConversation_ReturnsExistingIdWithoutDuplicateSpam()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Proposal.Status = 2; // Shortlisted
        var existingConvId = Guid.NewGuid();
        fixture.Conversations.Add(new Conversation
        {
            ConversationsId = existingConvId,
            ConversationType = (int)ConversationType.JobNegotiation,
            JobPostsId = fixture.JobPostId,
            ProposalsId = fixture.ProposalId,
            ContractsId = fixture.ContractId,
            CreatedByUserId = fixture.ClientUserId,
            Status = (int)ConversationStatus.Active,
            CreatedAt = fixture.Now
        });
        fixture.Participants.Add(new ConversationParticipant
        {
            ConversationsId = existingConvId,
            UserId = fixture.ClientUserId,
            ParticipantRole = (int)ParticipantRole.Client
        });
        fixture.Participants.Add(new ConversationParticipant
        {
            ConversationsId = existingConvId,
            UserId = fixture.FreelancerUserId,
            ParticipantRole = (int)ParticipantRole.Freelancer
        });

        var handler = CreateHandler(fixture);
        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, fixture.ClientUserId);

        // Act
        var conversationId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingConvId, conversationId);
        
        // No status change from shortlisted
        Assert.Equal(2, fixture.Proposal.Status);

        // No new notifications/emails should be triggered
        await _notificationService.DidNotReceive().CreateNotificationAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        //await _emailService.DidNotReceive().SendEmailAsync(
        //    Arg.Any<Application.Features.Auth.Shared.DTOs.EmailRequest>(),
        //    Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonOwnerClient_ThrowsForbiddenAccessException()
    {
        // Arrange
        var fixture = new TestFixture();
        var otherClientUserId = Guid.NewGuid();
        fixture.Context.AddSet(new ClientProfile { ClientProfilesId = Guid.NewGuid(), UserId = otherClientUserId });

        var handler = CreateHandler(fixture);
        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, otherClientUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_FreelancerAccepting_ThrowsForbiddenAccessException()
    {
        // Arrange
        var fixture = new TestFixture();
        var handler = CreateHandler(fixture);
        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, fixture.FreelancerUserId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ClosedJobPost_ThrowsBadRequestException()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.JobPost.Status = 2; // Closed
        var handler = CreateHandler(fixture);
        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, fixture.ClientUserId);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Theory]
    [InlineData(4)] // Rejected
    [InlineData(5)] // Withdrawn
    public async Task Handle_RejectedOrWithdrawnProposal_ThrowsBadRequestException(int invalidStatus)
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.Proposal.Status = invalidStatus;
        var handler = CreateHandler(fixture);
        var command = new AcceptProposalForNegotiationCommand(fixture.ProposalId, fixture.ClientUserId);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
    }

    private AcceptProposalForNegotiationCommandHandler CreateHandler(TestFixture fixture)
    {
        return new AcceptProposalForNegotiationCommandHandler(
            fixture.Context,
            _dateTimeService,
            _chatRealtimeNotifier,
            _notificationService,
            _emailService,
            _emailRenderer,
            _configuration,
            _logger);
    }

    private sealed class TestFixture
    {
        public TestFixture()
        {
            var clientUser = new User
            {
                UserId = ClientUserId,
                Role = (int)UserRole.Client,
                Email = "client@example.com",
                FullName = "Client User"
            };
            var freelancerUser = new User
            {
                UserId = FreelancerUserId,
                Role = (int)UserRole.Freelancer,
                Email = "freelancer@example.com",
                FullName = "Freelancer User"
            };
            var clientProfile = new ClientProfile
            {
                ClientProfilesId = ClientProfileId,
                UserId = ClientUserId,
                User = clientUser
            };
            var freelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = FreelancerProfileId,
                UserId = FreelancerUserId,
                User = freelancerUser
            };

            JobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Test Job Title",
                Description = "Description of test job",
                Status = 1,
                CreatedAt = Now,
                ClientProfiles = clientProfile
            };
            Proposal = new Proposal
            {
                ProposalsId = ProposalId,
                JobPostsId = JobPostId,
                FreelancerProfilesId = FreelancerProfileId,
                ProposedBudget = 1500m,
                ProposedDuration = "2 weeks",
                Status = 1, // Pending
                JobPosts = JobPost,
                FreelancerProfiles = freelancerProfile
            };
            Proposal.ProposalMilestonePlans.Add(new ProposalMilestonePlan
            {
                ProposalMilestonePlansId = Guid.NewGuid(),
                ProposalsId = ProposalId,
                Title = "Foundation",
                Amount = 600m,
                OrderIndex = 0
            });
            Proposal.ProposalMilestonePlans.Add(new ProposalMilestonePlan
            {
                ProposalMilestonePlansId = Guid.NewGuid(),
                ProposalsId = ProposalId,
                Title = "Final delivery",
                Amount = 900m,
                OrderIndex = 1
            });
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Test Job Title",
                TotalBudget = 1500m,
                Status = (int)ContractStatus.PendingFreelancerSelection,
                CreatedAt = Now
            };

            Context.AddSet(clientUser, freelancerUser);
            Context.AddSet(clientProfile);
            Context.AddSet(freelancerProfile);
            Context.AddSet(JobPost);
            Context.AddSet(Proposal);
            Contracts = Context.AddSet(Contract);
            Conversations = Context.AddSet<Conversation>();
            Participants = Context.AddSet<ConversationParticipant>();
            Milestones = Context.AddSet<Milestone>();
            NegotiationDrafts = Context.AddSet<NegotiationMilestoneDraft>();
            EscrowTransactions = Context.AddSet<EscrowTransaction>();
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
        public TestDbSet<Contract> Contracts { get; }
        public TestDbSet<Conversation> Conversations { get; }
        public TestDbSet<ConversationParticipant> Participants { get; }
        public TestDbSet<Milestone> Milestones { get; }
        public TestDbSet<NegotiationMilestoneDraft> NegotiationDrafts { get; }
        public TestDbSet<EscrowTransaction> EscrowTransactions { get; }
        public JobPost JobPost { get; }
        public Proposal Proposal { get; }
        public Contract Contract { get; }
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
