using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Details.Client.Submit.Commands;
using Application.Features.Contracts.Details.Freelancer.Confirm.Commands;
using Application.Features.Contracts.Escrow.Client.Fund.Commands;
using Application.Features.Contracts.MilestoneReview.Freelancer.Accept.Commands;
using Application.Features.Contracts.MilestoneReview.Freelancer.RequestChange.Commands;
using Application.Features.Contracts.Signing.Common.Sign.Commands;
using Application.Features.Contracts.Signing.Common.Sign.DTOs;
using Application.Features.Contracts.Details.Freelancer.RequestChange.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class ContractWorkflowTests
{
    private const string SignatureDataUri = "data:image/png;base64,aGVsbG8=";
    
    [Fact]
    public async Task SubmitAndFreelancerConfirm_CreatesFullEscrowAndMovesToPendingSignature()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.ApplyValidDetails();

        var submitHandler = new SubmitContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());
        await submitHandler.Handle(
            new SubmitContractDetailsCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingContractConfirmation, fixture.Contract.Status);

        var confirmHandler = new ConfirmContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)),
            new NoopChatRealtimeNotifier());

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
            new FixedDateTimeService(fixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

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
    public async Task FundEscrow_FullySignedPendingSignatureSelfHealsAndFunds()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.MarkDocumentFullySigned();
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 1_000m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        var handler = new FundContractEscrowCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        var result = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, result.ContractStatus);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal(1_000_000m, result.RequiredAmountVnd);
        Assert.Equal(1_000m, result.HeldTokens);
        Assert.Equal(0m, fixture.Wallets.Entities[0].AvailableTokens);
        Assert.Equal(1_000m, fixture.Wallets.Entities[0].HeldTokens);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal(fixture.Contract.TotalBudget, fixture.Escrows.Entities[0].RequiredAmount);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task FundEscrow_StuckPendingSignatureBridgesClientJobPostSignatureAndFunds()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.AddSignedJobPostDocument();
        fixture.AddFreelancerContractSignature();
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 1_000m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        var handler = new FundContractEscrowCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        var result = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        var contractDocument = fixture.GetContractDocument();
        var contractSignatures = fixture.EsignSignatures.Entities
            .Where(signature => signature.EsignDocumentsId == contractDocument.EsignDocumentsId)
            .ToList();

        Assert.Equal((int)ContractStatus.Active, result.ContractStatus);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, contractDocument.Status);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.ClientUserId &&
            signature.SignerRole == (int)ESignerRole.Client &&
            signature.SignatureImageUrl == fixture.ClientSignatureUrl);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.FreelancerUserId &&
            signature.SignerRole == (int)ESignerRole.Freelancer);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Single(fixture.WalletTransactions.Entities);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task FundEscrow_PendingSignatureStillRequiresClientAndFreelancerSignatures()
    {
        var missingFreelancerFixture = new ContractWorkflowFixture();
        missingFreelancerFixture.MoveToPendingSignatureWithDocument();
        missingFreelancerFixture.AddSignedJobPostDocument();

        var missingFreelancerHandler = new FundContractEscrowCommandHandler(
            missingFreelancerFixture.Context,
            new FixedDateTimeService(missingFreelancerFixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            missingFreelancerHandler.Handle(
                new FundContractEscrowCommand(missingFreelancerFixture.ContractId, missingFreelancerFixture.ClientUserId),
                CancellationToken.None));

        var missingClientFixture = new ContractWorkflowFixture();
        missingClientFixture.MoveToPendingSignatureWithDocument();
        missingClientFixture.AddFreelancerContractSignature();

        var missingClientHandler = new FundContractEscrowCommandHandler(
            missingClientFixture.Context,
            new FixedDateTimeService(missingClientFixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            missingClientHandler.Handle(
                new FundContractEscrowCommand(missingClientFixture.ContractId, missingClientFixture.ClientUserId),
                CancellationToken.None));

        Assert.Equal((int)ContractStatus.PendingSignature, missingFreelancerFixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingSignature, missingClientFixture.Contract.Status);
    }

    [Fact]
    public async Task SignContract_FullySignedMovesToPendingEscrowFunding()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();

        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            fixture.MediaService);

        await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                new SignContractRequest(SignatureDataUri, 300, 100),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.PartiallySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal(fixture.ClientSignatureUrl, fixture.EsignSignatures.Entities[0].SignatureImageUrl);
        Assert.Equal("esign/signatures", fixture.MediaService.Uploads[0].Folder);
        Assert.Equal("image/png", fixture.MediaService.Uploads[0].ContentType);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest(SignatureDataUri, null, null),
                    null,
                    null),
                CancellationToken.None));

        var result = await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new SignContractRequest(SignatureDataUri, 300, 100),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingEscrow, fixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingEscrow, result.Status);
        Assert.Equal(fixture.Escrows.Entities[0].ContractEscrowId, result.EscrowId);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, fixture.Escrows.Entities[0].Status);
        Assert.Equal(fixture.Contract.TotalBudget, fixture.Escrows.Entities[0].RequiredAmount);
        Assert.Equal(1.0m, fixture.Escrows.Entities[0].RequiredPercentage);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Equal((int)ConversationType.JobNegotiation, fixture.Conversation.ConversationType);
        Assert.Equal(2, fixture.EsignSignatures.Entities.Count);
        Assert.Equal(fixture.FreelancerSignatureUrl, fixture.EsignSignatures.Entities[1].SignatureImageUrl);
        Assert.Equal(2, fixture.MediaService.Uploads.Count);
    }

    [Fact]
    public async Task SignContract_FreelancerSignatureBridgesClientJobPostSignatureAndMovesToEscrow()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.AddSignedJobPostDocument();
        var mediaService = new FakeMediaService(fixture.FreelancerSignatureUrl);

        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            mediaService);

        var result = await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new SignContractRequest(SignatureDataUri, 300, 100),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        var contractDocument = fixture.GetContractDocument();
        var contractSignatures = fixture.EsignSignatures.Entities
            .Where(signature => signature.EsignDocumentsId == contractDocument.EsignDocumentsId)
            .ToList();

        Assert.Equal((int)ContractStatus.PendingEscrow, fixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingEscrow, result.Status);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, contractDocument.Status);
        Assert.Equal(fixture.Escrows.Entities[0].ContractEscrowId, result.EscrowId);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.ClientUserId &&
            signature.SignerRole == (int)ESignerRole.Client &&
            signature.SignatureImageUrl == fixture.ClientSignatureUrl);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.FreelancerUserId &&
            signature.SignerRole == (int)ESignerRole.Freelancer &&
            signature.SignatureImageUrl == fixture.FreelancerSignatureUrl);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Single(mediaService.Uploads);
    }

    [Fact]
    public async Task SignContract_RejectsInvalidSignatureDataUri()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();

        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            fixture.MediaService);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest("not-base64", null, null),
                    null,
                    null),
                CancellationToken.None));

        Assert.Empty(fixture.MediaService.Uploads);
    }

    [Fact]
    public async Task AcceptContractMilestones_FullySignedContractMovesToPendingEscrow()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.MarkDocumentFullySigned();
        var waitlistedUserId = Guid.NewGuid();
        var waitlistedFreelancerProfile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = waitlistedUserId
        };
        var waitlistedProposal = new Proposal
        {
            ProposalsId = Guid.NewGuid(),
            JobPostsId = fixture.JobPostId,
            FreelancerProfilesId = waitlistedFreelancerProfile.FreelancerProfilesId,
            FreelancerProfiles = waitlistedFreelancerProfile,
            Status = 1
        };
        fixture.Context.Set<FreelancerProfile>().Add(waitlistedFreelancerProfile);
        fixture.Context.Set<Proposal>().Add(waitlistedProposal);
        var notificationService = new RecordingNotificationService();

        var handler = new AcceptContractMilestonesCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            notificationService);

        var result = await handler.Handle(
            new AcceptContractMilestonesCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingEscrow, fixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingEscrow, result.Status);
        Assert.Equal(fixture.Escrows.Entities[0].ContractEscrowId, result.EscrowId);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, fixture.Escrows.Entities[0].Status);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Equal((int)ConversationType.ContractWorkroom, fixture.Conversation.ConversationType);
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(waitlistedUserId, notification.UserId);
        Assert.Equal(NotificationType.ProposalStatusChanged, notification.Type);
        Assert.Equal(waitlistedProposal.ProposalsId, notification.ReferenceId);
    }

    [Fact]
    public async Task RequestContractMilestoneChange_VoidsSignedDocumentAndReturnsToDetails()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.MarkDocumentFullySigned();
        var notificationService = new RecordingNotificationService();

        var handler = new RequestContractMilestoneChangeCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            notificationService);

        var result = await handler.Handle(
            new RequestContractMilestoneChangeCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new RequestContractDetailsChangeRequest("Please adjust the second milestone.")),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingContractDetails, fixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingContractDetails, result.Status);
        Assert.Equal((int)ESignDocumentStatus.Voided, fixture.EsignDocuments.Entities[0].Status);
        Assert.All(fixture.EsignSignatures.Entities, signature =>
            Assert.Equal((int)ESignSignatureStatus.Declined, signature.Status));
        var notification = Assert.Single(notificationService.Notifications);
        Assert.Equal(fixture.ClientUserId, notification.UserId);
        Assert.Equal(NotificationType.MilestoneUpdated, notification.Type);
        Assert.Equal("Milestone change requested", notification.Title);
        Assert.Contains("Please adjust the second milestone.", notification.Content);
        Assert.Equal(fixture.ContractId, notification.ReferenceId);
        Assert.Equal("Contract", notification.ReferenceType);
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
        public string ClientSignatureUrl { get; } = "https://res.cloudinary.com/gigbridge/esign/signatures/client.png";
        public string FreelancerSignatureUrl { get; } = "https://res.cloudinary.com/gigbridge/esign/signatures/freelancer.png";
        public FakeMediaService MediaService { get; } = new(
            "https://res.cloudinary.com/gigbridge/esign/signatures/client.png",
            "https://res.cloudinary.com/gigbridge/esign/signatures/freelancer.png");
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

 
        public void ApplyValidDetails()
        {

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

        public EsignDocument GetContractDocument()
        {
            return EsignDocuments.Entities.Single(document => document.ContractsId == ContractId);
        }

        public void AddSignedJobPostDocument()
        {
            var templateId = AddTemplate();
            var documentId = Guid.NewGuid();

            EsignDocuments.Add(new EsignDocument
            {
                EsignDocumentsId = documentId,
                EsignTemplatesId = templateId,
                JobPostsId = JobPostId,
                ContractsId = null,
                DocumentCode = "GB-JOB-TEST",
                RenderedHtmlContent = "<html>job post contract</html>",
                Status = (int)ESignDocumentStatus.FullySigned,
                FinalizedAt = Now,
                CreatedAt = Now
            });

            EsignSignatures.Add(new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = documentId,
                UserId = ClientUserId,
                SignerRole = (int)ESignerRole.Client,
                SignatureImageUrl = ClientSignatureUrl,
                SignatureWidth = 300,
                SignatureHeight = 100,
                Status = (int)ESignSignatureStatus.Signed,
                SignedAt = Now,
                IpAddress = "127.0.0.1",
                UserAgent = "test",
                CreatedAt = Now
            });
        }

        public void AddFreelancerContractSignature()
        {
            var contractDocument = GetContractDocument();

            contractDocument.Status = (int)ESignDocumentStatus.PartiallySigned;
            contractDocument.UpdatedAt = Now;

            EsignSignatures.Add(new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = contractDocument.EsignDocumentsId,
                UserId = FreelancerUserId,
                SignerRole = (int)ESignerRole.Freelancer,
                SignatureImageUrl = FreelancerSignatureUrl,
                SignatureWidth = 300,
                SignatureHeight = 100,
                Status = (int)ESignSignatureStatus.Signed,
                SignedAt = Now,
                IpAddress = "127.0.0.1",
                UserAgent = "test",
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
            MarkDocumentFullySigned();
        }

        public void MarkDocumentFullySigned()
        {
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

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NotificationCall> Notifications { get; } = [];

        public Task CreateNotificationAsync(
            Guid userId,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(new NotificationCall(userId, type, title, content, referenceId, referenceType));
            return Task.CompletedTask;
        }

        public Task CreateBroadcastNotificationAsync(
            NotificationTarget target,
            NotificationType type,
            string title,
            string? content = null,
            Guid? referenceId = null,
            string? referenceType = null,
            Guid? targetUserId = null,
            bool sendEmail = false,
            Guid? createdByAdminId = null,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record NotificationCall(
        Guid UserId,
        NotificationType Type,
        string Title,
        string? Content,
        Guid? ReferenceId,
        string? ReferenceType);

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
