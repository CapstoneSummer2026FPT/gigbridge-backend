using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Details.Client.Submit.Commands;
using Application.Features.Contracts.Details.Client.Update.Commands;
using Application.Features.Contracts.Details.Client.Update.DTOs;
using Application.Features.Contracts.Details.Freelancer.Confirm.Commands;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Application.Features.Contracts.Signing.Common.Sign.Commands;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class ContractWorkflowTests
{
    [Fact]
    public async Task UpdateDetails_ClientOnlyAndMilestoneSumMustEqualBudget()
    {
        var fixture = new ContractWorkflowFixture();
        var handler = new UpdateContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new UpdateContractDetailsCommand(
                    fixture.ContractId,
                    fixture.FreelancerUserId,
                    fixture.ValidDetails()),
                CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateContractDetailsCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    fixture.InvalidMilestoneTotalDetails()),
                CancellationToken.None));

        await handler.Handle(
            new UpdateContractDetailsCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                fixture.ValidDetails()),
            CancellationToken.None);

        Assert.Equal("Build production release", fixture.Contract.ScopeOfWork);
        Assert.Equal(2, fixture.Milestones.Entities.Count);
    }

    [Fact]
    public async Task SubmitAndFreelancerConfirm_CreatesFullEscrowAndMovesToPendingSignature()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.ApplyValidDetails();

        var submitHandler = new SubmitContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));
        await submitHandler.Handle(
            new SubmitContractDetailsCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingContractConfirmation, fixture.Contract.Status);

        var confirmHandler = new ConfirmContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)));

        await confirmHandler.Handle(
            new ConfirmContractDetailsCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            confirmHandler.Handle(
                new ConfirmContractDetailsCommand(fixture.ContractId, fixture.FreelancerUserId),
                CancellationToken.None));

        var escrow = Assert.Single(fixture.Escrows.Entities);
        Assert.Equal(1_000_000m, escrow.RequiredAmount);
        Assert.Equal(1.0m, escrow.RequiredPercentage);
        Assert.Equal(0m, escrow.FundedAmount);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, escrow.Status);
        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
    }

    [Fact]
    public async Task FundEscrow_RequiresFullySignedContractAndFundsOneHundredPercent()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();

        var handler = new FundContractEscrowCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        fixture.MoveToFullySignedPendingEscrow();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 900m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        fixture.Wallets.Entities[0].AvailableTokens = 1_000m;

        var result = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(1_000_000m, result.RequiredAmountVnd);
        Assert.Equal(1_000m, result.HeldTokens);
        Assert.Equal(0m, fixture.Wallets.Entities[0].AvailableTokens);
        Assert.Equal(1_000m, fixture.Wallets.Entities[0].HeldTokens);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Single(fixture.EsignDocuments.Entities);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task SignContract_ConvertsWorkroomButWaitsForEscrowFunding()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();

        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                new SignContractRequest("https://sig/client.png", 300, 100),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.PartiallySigned, fixture.EsignDocuments.Entities[0].Status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest("https://sig/client.png", null, null),
                    null,
                    null),
                CancellationToken.None));

        await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new SignContractRequest("https://sig/freelancer.png", 300, 100),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingEscrow, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal((int)ConversationType.ContractWorkroom, fixture.Conversation.ConversationType);
        Assert.Equal(2, fixture.EsignSignatures.Entities.Count);
    }

    private sealed class ContractWorkflowFixture
    {
        public ContractWorkflowFixture()
        {
            Contract = new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "Fixed contract",
                TotalBudget = 1_000_000m,
                Status = (int)ContractStatus.PendingContractDetails,
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

            Context.AddSet(
                new User { UserId = AdminUserId, Role = (int)UserRole.Admin, Email = "admin@example.com", FullName = "Admin" },
                new User { UserId = ClientUserId, Role = (int)UserRole.Client, Email = "client@example.com", FullName = "Client" },
                new User { UserId = FreelancerUserId, Role = (int)UserRole.Freelancer, Email = "freelancer@example.com", FullName = "Freelancer" });
            Context.AddSet(new ClientProfile { ClientProfilesId = ClientProfileId, UserId = ClientUserId });
            Context.AddSet(new FreelancerProfile { FreelancerProfilesId = FreelancerProfileId, UserId = FreelancerUserId });
            Context.AddSet(new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Fixed job",
                Description = "Build it",
                Status = 1,
                CreatedAt = Now
            });
            Context.AddSet(Contract);
            Context.AddSet(Conversation);
            Context.AddSet<Message>();
            Context.AddSet<ConversationParticipant>();
            Milestones = Context.AddSet<Milestone>();
            Escrows = Context.AddSet<ContractEscrow>();
            Wallets = Context.AddSet<UserWallet>();
            WalletTransactions = Context.AddSet<WalletTransaction>();
            EscrowTransactions = Context.AddSet<EscrowTransaction>();
            EsignTemplates = Context.AddSet<EsignTemplate>();
            EsignDocuments = Context.AddSet<EsignDocument>();
            EsignSignatures = Context.AddSet<EsignSignature>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();
        public DateTime Now { get; } = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Guid WalletId { get; } = Guid.NewGuid();
        public Contract Contract { get; }
        public Conversation Conversation { get; }
        public TestDbSet<Milestone> Milestones { get; }
        public TestDbSet<ContractEscrow> Escrows { get; }
        public TestDbSet<UserWallet> Wallets { get; }
        public TestDbSet<WalletTransaction> WalletTransactions { get; }
        public TestDbSet<EscrowTransaction> EscrowTransactions { get; }
        public TestDbSet<EsignTemplate> EsignTemplates { get; }
        public TestDbSet<EsignDocument> EsignDocuments { get; }
        public TestDbSet<EsignSignature> EsignSignatures { get; }

        public UpdateContractDetailsRequest ValidDetails()
        {
            return new UpdateContractDetailsRequest(
                "Build production release",
                "Pay through escrow milestones",
                "Client owns final deliverables after full payment",
                "Both parties keep project data confidential",
                "Cancellation requires written agreement",
                "Disputes are handled through GigBridge",
                new[]
                {
                    new ContractMilestoneRequest(null, "Milestone 1", 400_000m, null, 0),
                    new ContractMilestoneRequest(null, "Milestone 2", 600_000m, null, 1)
                });
        }

        public UpdateContractDetailsRequest InvalidMilestoneTotalDetails()
        {
            return ValidDetails() with
            {
                Milestones = new[]
                {
                    new ContractMilestoneRequest(null, "Only milestone", 500_000m, null, 0)
                }
            };
        }

        public void ApplyValidDetails()
        {

            Contract.DisputeTerms = "Disputes are handled through GigBridge";
            Milestones.Add(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 400_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            });
            Milestones.Add(new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = 600_000m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            });
        }

        public void MoveToPendingSignature()
        {
            ApplyValidDetails();
            Contract.Status = (int)ContractStatus.PendingSignature;
            Escrows.Add(new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000_000m,
                FundedAmount = 0m,
                RequiredPercentage = 1.0m,
                Currency = "VND",
                Status = (int)ContractEscrowStatus.PendingFunding,
                CreatedAt = Now
            });
        }

        public void MoveToPendingSignatureWithDocument()
        {
            MoveToPendingSignature();
            var templateId = AddTemplate();
            EsignDocuments.Add(new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                EsignTemplatesId = templateId,
                JobPostsId = JobPostId,
                ContractsId = ContractId,
                DocumentCode = "GB-TEST",
                RenderedHtmlContent = "<html>contract</html>",
                Status = (int)ESignDocumentStatus.PendingSignatures,
                CreatedAt = Now
            });
        }

        public void MoveToFullySignedPendingEscrow()
        {
            if (EsignDocuments.Entities.Count == 0)
            {
                MoveToPendingSignatureWithDocument();
            }

            Contract.Status = (int)ContractStatus.PendingEscrow;
            EsignDocuments.Entities[0].Status = (int)ESignDocumentStatus.FullySigned;
            EsignDocuments.Entities[0].FinalizedAt = Now;
            EsignSignatures.Add(new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = EsignDocuments.Entities[0].EsignDocumentsId,
                UserId = ClientUserId,
                SignerRole = (int)ESignerRole.Client,
                Status = (int)ESignSignatureStatus.Signed,
                SignedAt = Now,
                CreatedAt = Now
            });
            EsignSignatures.Add(new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = EsignDocuments.Entities[0].EsignDocumentsId,
                UserId = FreelancerUserId,
                SignerRole = (int)ESignerRole.Freelancer,
                Status = (int)ESignSignatureStatus.Signed,
                SignedAt = Now,
                CreatedAt = Now
            });
        }

        public Guid AddTemplate()
        {
            var templateId = Guid.NewGuid();
            EsignTemplates.Add(new EsignTemplate
            {
                EsignTemplatesId = templateId,
                Name = "Fixed price contract",
                TemplateCode = "CONTRACT_FIXED_PRICE",
                HtmlContent = "<html>{{Contract.Title}}<table>{{MilestonesHtml}}</table></html>",
                Version = 1,
                IsActive = true,
                CreatedBy = AdminUserId,
                CreatedAt = Now
            });

            return templateId;
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
