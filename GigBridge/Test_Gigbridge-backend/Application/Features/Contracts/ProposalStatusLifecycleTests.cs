using Application.Common.Interfaces.Email;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.InternalServices.Notifications.Interfaces;
using Application.Common.InternalServices.Proposals.Email;
using Application.Common.InternalServices.Proposals.Interfaces;
using Application.Common.InternalServices.Proposals.Models;
using Application.Features.Contracts.Cancellation.Common.Cancel.Commands;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Application.Features.Proposals.Common.AcceptForNegotiation.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Escrow;
using Domain.Enums.ESign;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts;

/// <summary>
/// Reproduces the worked example from the "Move Proposal.Accepted to Escrow-Funding Time"
/// change end-to-end: negotiate P1, cancel its contract, negotiate P3, fund P3's contract to
/// Active, and assert every proposal for the job post lands exactly where the business rule
/// says it should.
/// </summary>
public class ProposalStatusLifecycleTests
{
    [Fact]
    public async Task NegotiateP1_CancelP1_NegotiateP3_FundP3Escrow_MatchesWorkedExample()
    {
        var fixture = new LifecycleFixture();
        var now = fixture.Now;

        // Initial state: P1=Pending, P2=Shortlisted, P3=Shortlisted, P4=Pending, P5=Rejected, P6=Pending.
        Assert.Equal(1, fixture.P1.Status);
        Assert.Equal(2, fixture.P2.Status);
        Assert.Equal(2, fixture.P3.Status);
        Assert.Equal(1, fixture.P4.Status);
        Assert.Equal(4, fixture.P5.Status);
        Assert.Equal(1, fixture.P6.Status);

        var negotiateHandler = fixture.CreateNegotiateHandler();

        // Client starts negotiation with P1: Pending -> Shortlisted.
        await negotiateHandler.Handle(
            new AcceptProposalForNegotiationCommand(fixture.P1.ProposalsId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal(2, fixture.P1.Status);

        // Contract is created for P1 (constructed directly, as RespondFinalOfferCommandHandler
        // would, without touching Proposal.Status under the new flow) and reaches PendingSignature.
        var p1Contract = fixture.AddContract(fixture.P1, fixture.F1ProfileId, ContractStatus.PendingSignature, now.AddMinutes(-5));

        // Client cancels because the freelancer is taking too long.
        var cancelHandler = fixture.CreateCancelHandler();
        await cancelHandler.Handle(
            new CancelContractCommand(p1Contract.ContractsId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Cancelled, p1Contract.Status);
        // P1 stays Shortlisted — cancellation never advanced it past Shortlisted, so there is
        // nothing to restore.
        Assert.Equal(2, fixture.P1.Status);
        Assert.Equal(2, fixture.P2.Status);
        Assert.Equal(2, fixture.P3.Status);
        Assert.Equal(1, fixture.P4.Status);
        Assert.Equal(4, fixture.P5.Status);
        Assert.Equal(1, fixture.P6.Status);

        // Client can now negotiate with P3 (already Shortlisted) — the job post's earlier
        // Cancelled contract no longer blocks a new one for the same JobPost.
        await negotiateHandler.Handle(
            new AcceptProposalForNegotiationCommand(fixture.P3.ProposalsId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal(2, fixture.P3.Status); // already Shortlisted — unchanged

        // A new contract is created for P3 and reaches PendingEscrow, fully signed, with a
        // funded escrow (simulating the state right before FundContractEscrowCommandHandler's
        // self-heal completion runs).
        var p3Contract = fixture.AddContract(fixture.P3, fixture.F3ProfileId, ContractStatus.PendingEscrow, now.AddMinutes(-10));
        fixture.AddFullySignedDocument(p3Contract.ContractsId);
        fixture.AddFundedEscrow(p3Contract.ContractsId);

        var fundHandler = fixture.CreateFundEscrowHandler();
        var result = await fundHandler.Handle(
            new FundContractEscrowCommand(p3Contract.ContractsId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, result.ContractStatus);
        Assert.Equal((int)ContractStatus.Active, p3Contract.Status);

        // Final state matches the worked example exactly:
        // P1=Rejected(via sibling reject), P2=Rejected, P3=Accepted, P4=Rejected, P5=Rejected, P6=Rejected.
        Assert.Equal(4, fixture.P1.Status);
        Assert.Equal(4, fixture.P2.Status);
        Assert.Equal(3, fixture.P3.Status);
        Assert.Equal(4, fixture.P4.Status);
        Assert.Equal(4, fixture.P5.Status);
        Assert.Equal(4, fixture.P6.Status);
    }

    private sealed class LifecycleFixture
    {
        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid F1ProfileId { get; } = Guid.NewGuid();
        public Guid F3ProfileId { get; } = Guid.NewGuid();
        public Guid EsignTemplateId { get; } = Guid.NewGuid();

        public Proposal P1 { get; }
        public Proposal P2 { get; }
        public Proposal P3 { get; }
        public Proposal P4 { get; }
        public Proposal P5 { get; }
        public Proposal P6 { get; }

        private readonly TestDbSet<Contract> _contracts;
        private readonly TestDbSet<EsignDocument> _esignDocuments;
        private readonly TestDbSet<ContractEscrow> _escrows;

        public LifecycleFixture()
        {
            var clientUser = new User
            {
                UserId = ClientUserId,
                Role = (int)UserRole.Client,
                Email = "client@example.com",
                FullName = "Client User"
            };
            var clientProfile = new ClientProfile
            {
                ClientProfilesId = ClientProfileId,
                UserId = ClientUserId,
                User = clientUser
            };
            var jobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Build a landing page",
                Description = "Build it",
                Status = 1,
                CreatedAt = Now
            };

            Context.AddSet(clientUser);
            Context.AddSet(clientProfile);
            Context.AddSet(jobPost);
            Context.AddSet<Conversation>();
            Context.AddSet<ConversationParticipant>();
            Context.AddSet<Message>();
            Context.AddSet<NegotiationMilestoneDraft>();
            Context.AddSet<Milestone>();
            Context.AddSet<EsignTemplate>().Add(new EsignTemplate
            {
                EsignTemplatesId = EsignTemplateId,
                Name = "Fixed price contract",
                TemplateCode = "CONTRACT_FIXED_PRICE",
                HtmlContent = "<html></html>",
                Version = 1,
                IsActive = true,
                CreatedAt = Now
            });
            _contracts = Context.AddSet<Contract>();
            _esignDocuments = Context.AddSet<EsignDocument>();
            _escrows = Context.AddSet<ContractEscrow>();

            P1 = CreateProposal(1, jobPost);
            P2 = CreateProposal(2, jobPost);
            P3 = CreateProposal(2, jobPost);
            P4 = CreateProposal(1, jobPost);
            P5 = CreateProposal(4, jobPost);
            P6 = CreateProposal(1, jobPost);

            // P1 and P3 are the two proposals this scenario negotiates with, so they need a
            // resolvable FreelancerProfile/User for AcceptProposalForNegotiationCommandHandler.
            WireFreelancer(P1, F1ProfileId);
            WireFreelancer(P3, F3ProfileId);
            WireFreelancer(P2, Guid.NewGuid());
            WireFreelancer(P4, Guid.NewGuid());
            WireFreelancer(P5, Guid.NewGuid());
            WireFreelancer(P6, Guid.NewGuid());
        }

        private Proposal CreateProposal(int status, JobPost jobPost)
        {
            var proposal = new Proposal
            {
                ProposalsId = Guid.NewGuid(),
                JobPostsId = JobPostId,
                Status = status,
                JobPosts = jobPost,
                SubmittedAt = Now
            };
            Context.Set<Proposal>().Add(proposal);
            return proposal;
        }

        private void WireFreelancer(Proposal proposal, Guid freelancerProfileId)
        {
            var freelancerUserId = Guid.NewGuid();
            var freelancerUser = new User
            {
                UserId = freelancerUserId,
                Role = (int)UserRole.Freelancer,
                Email = $"freelancer-{freelancerProfileId:N}@example.com",
                FullName = "Freelancer User"
            };
            var freelancerProfile = new FreelancerProfile
            {
                FreelancerProfilesId = freelancerProfileId,
                UserId = freelancerUserId,
                User = freelancerUser
            };
            Context.Set<User>().Add(freelancerUser);
            Context.Set<FreelancerProfile>().Add(freelancerProfile);
            proposal.FreelancerProfilesId = freelancerProfileId;
            proposal.FreelancerProfiles = freelancerProfile;
        }

        public Contract AddContract(Proposal proposal, Guid freelancerProfileId, ContractStatus status, DateTime createdAt)
        {
            var contract = new Contract
            {
                ContractsId = Guid.NewGuid(),
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = freelancerProfileId,
                ProposalsId = proposal.ProposalsId,
                Title = "Build a landing page",
                TotalBudget = 500m,
                Status = (int)status,
                RevisionNumber = 1,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            _contracts.Add(contract);
            return contract;
        }

        public void AddFullySignedDocument(Guid contractId)
        {
            _esignDocuments.Add(new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                EsignTemplatesId = EsignTemplateId,
                JobPostsId = JobPostId,
                ContractsId = contractId,
                DocumentCode = $"GB-{contractId:N}",
                Status = (int)ESignDocumentStatus.FullySigned,
                FinalizedAt = Now,
                CreatedAt = Now
            });
        }

        public void AddFundedEscrow(Guid contractId)
        {
            _escrows.Add(new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = contractId,
                RequiredAmount = 500m,
                FundedAmount = 500m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.Funded,
                CreatedAt = Now,
                FundedAt = Now
            });
        }

        public AcceptProposalForNegotiationCommandHandler CreateNegotiateHandler()
        {
            var configuration = Substitute.For<IConfiguration>();
            configuration["FrontendBaseUrl"].Returns("http://localhost:5173");
            var emailRenderer = Substitute.For<IProposalNegotiationEmailRenderer>();
            emailRenderer.Render(Arg.Any<ProposalNegotiationEmailModel>())
                .Returns(new RenderedProposalNegotiationEmail("Subject", "HtmlBody", "TextBody"));

            return new AcceptProposalForNegotiationCommandHandler(
                Context,
                new FixedDateTimeService(Now),
                Substitute.For<IChatRealtimeNotifier>(),
                Substitute.For<INotificationService>(),
                Substitute.For<IEmailService>(),
                emailRenderer,
                configuration,
                NullLogger<AcceptProposalForNegotiationCommandHandler>.Instance);
        }

        public CancelContractCommandHandler CreateCancelHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new CapturingChatRealtimeNotifier(),
                new NoopNotificationService(),
                new CapturingUserAuditLogService(),
                NullLogger<CancelContractCommandHandler>.Instance);

        public FundContractEscrowCommandHandler CreateFundEscrowHandler() =>
            new(
                Context,
                new FixedDateTimeService(Now),
                new NoopNotificationService(),
                new NoopChatRealtimeNotifier(),
                new CapturingUserAuditLogService(),
                NullLogger<FundContractEscrowCommandHandler>.Instance);
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
