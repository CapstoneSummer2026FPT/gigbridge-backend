using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Details.Client.Update.Commands;
using Application.Features.Contracts.Details.Client.Update.DTOs;
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
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Contracts.Common;

public class ContractWorkflowTests
{
    private const string SignatureDataUri = "data:image/png;base64,aGVsbG8=";

    [Fact]
    public async Task UpdateContractDetails_MilestoneTotalExceedsContractBudget_ThrowsBadRequest()
    {
        var fixture = new ContractWorkflowFixture();
        var handler = new UpdateContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier());

        var request = new UpdateContractDetailsRequest(
            [
                new ContractMilestoneRequest(null, "Milestone 1", 1_000_001m, DateOnly.FromDateTime(fixture.Now.AddDays(7)), 0,
                    WorkItems: [new ContractWorkItemRequest(null, "Implementation", "Complete implementation.", "Source code", "1 week", 0)])
            ]);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new UpdateContractDetailsCommand(fixture.ContractId, fixture.ClientUserId, request),
                CancellationToken.None));

        Assert.Contains("cannot exceed contract total budget", exception.Message);
        Assert.Empty(fixture.Milestones.Entities);
    }

    [Fact]
    public async Task UpdateContractDetails_ReusesPersistedMilestoneAndWorkItemIds()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new GigbridgeDbContext(options);
        var createdAt = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);
        var now = createdAt.AddDays(1);
        var clientUserId = Guid.NewGuid();
        var clientProfileId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();

        context.Set<ClientProfile>().Add(new ClientProfile
        {
            ClientProfilesId = clientProfileId,
            UserId = clientUserId
        });
        context.Set<Contract>().Add(new Contract
        {
            ContractsId = contractId,
            ClientProfilesId = clientProfileId,
            Title = "Editable contract",
            TotalBudget = 100m,
            Status = (int)ContractStatus.PendingContractDetails,
            RevisionNumber = 0,
            CreatedAt = createdAt
        });
        context.Set<Milestone>().Add(new Milestone
        {
            MilestonesId = milestoneId,
            ContractsId = contractId,
            Title = "Original milestone",
            Amount = 100m,
            SortOrder = 0,
            Status = (int)MilestoneStatus.Pending,
            CreatedAt = createdAt
        });
        context.Set<ContractWorkItem>().Add(new ContractWorkItem
        {
            ContractWorkItemId = workItemId,
            MilestonesId = milestoneId,
            Title = "Original work item",
            Description = "Original description",
            OrderIndex = 0,
            Status = (int)ContractWorkItemStatus.Todo,
            CreatedAt = createdAt
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new UpdateContractDetailsCommandHandler(
            context,
            new FixedDateTimeService(now),
            new NoopChatRealtimeNotifier());
        var request = new UpdateContractDetailsRequest(
        [
            new ContractMilestoneRequest(
                milestoneId,
                "Updated milestone",
                100m,
                DateOnly.FromDateTime(now.AddDays(7)),
                0,
                WorkItems:
                [
                    new ContractWorkItemRequest(
                        workItemId,
                        "Updated work item",
                        "Updated description",
                        "Updated deliverable",
                        "1 week",
                        0)
                ])
        ]);

        await handler.Handle(
            new UpdateContractDetailsCommand(contractId, clientUserId, request),
            CancellationToken.None);
        context.ChangeTracker.Clear();

        var persistedMilestone = await context.Set<Milestone>()
            .Include(milestone => milestone.WorkItems)
            .SingleAsync(milestone => milestone.ContractsId == contractId);
        var persistedWorkItem = Assert.Single(persistedMilestone.WorkItems);
        Assert.Equal(milestoneId, persistedMilestone.MilestonesId);
        Assert.Equal("Updated milestone", persistedMilestone.Title);
        Assert.Equal(createdAt, persistedMilestone.CreatedAt);
        Assert.Equal(workItemId, persistedWorkItem.ContractWorkItemId);
        Assert.Equal("Updated work item", persistedWorkItem.Title);
        Assert.Equal("Updated description", persistedWorkItem.Description);
        Assert.Equal(createdAt, persistedWorkItem.CreatedAt);
        Assert.Equal(1, (await context.Set<Contract>().SingleAsync()).RevisionNumber);
    }
    
    [Fact]
    public async Task SubmitAndFreelancerConfirm_CreatesEsignDocumentAndPendingEscrow()
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
        fixture.AddTemplate();

        var confirmHandler = new ConfirmContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)),
            new NoopChatRealtimeNotifier(),
            fixture.DocumentGenerator);

        await confirmHandler.Handle(
            new ConfirmContractDetailsCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            confirmHandler.Handle(
                new ConfirmContractDetailsCommand(fixture.ContractId, fixture.FreelancerUserId),
                CancellationToken.None));

        var escrow = Assert.Single(fixture.Escrows.Entities);
        Assert.Equal(1_000m, escrow.RequiredAmount);
        Assert.Equal(0m, escrow.FundedAmount);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, escrow.Status);
        Assert.Single(fixture.EsignDocuments.Entities);
        Assert.NotNull(fixture.EsignDocuments.Entities[0].ContractSnapshotJson);
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
        fixture.Contract.TotalBudget = 1_000_000m;
        fixture.Escrows.Entities[0].RequiredAmount = 1_000_000m;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 1_009_999.9999m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        fixture.Wallets.Entities[0].AvailableTokens = 1_010_000m;

        var result = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(1_000_000m, result.RequiredAmountVnd);
        Assert.Equal(1_000_000m, result.HeldTokens);
        Assert.Equal(0m, fixture.Wallets.Entities[0].AvailableTokens);
        Assert.Equal(1_000_000m, fixture.Wallets.Entities[0].HeldTokens);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal((int)ContractStatus.Active, fixture.Contract.Status);
        Assert.Single(fixture.EsignDocuments.Entities);
        var fee = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Equal(10_000m, fee.TokenAmount);
        Assert.Equal(10_000m, fee.VndAmount);
        var hold = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowHold));
        Assert.Equal(1_000_000m, hold.TokenAmount);
        Assert.Equal(1_000_000m, hold.VndAmount);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task FundEscrow_DepositedFirstSpendingMixesBothPoolsIntoEscrowComposition()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToFullySignedPendingEscrow();
        fixture.Contract.TotalBudget = 1_000_000m;
        fixture.Escrows.Entities[0].RequiredAmount = 1_000_000m;
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 600_000m,
            WithdrawableTokens = 410_000m,
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

        // 1,000,000-token hold: 600,000 deposited + 400,000 earned (deposited spent first).
        var escrow = fixture.Escrows.Entities[0];
        Assert.Equal(600_000m, escrow.DepositedTokens);
        Assert.Equal(400_000m, escrow.EarnedTokens);

        // The 10,000-token service fee is charged after the hold from the remaining 10,000 earned.
        var wallet = fixture.Wallets.Entities[0];
        Assert.Equal(0m, wallet.AvailableTokens);
        Assert.Equal(0m, wallet.WithdrawableTokens);
        Assert.Equal(1_000_000m, wallet.HeldTokens);

        var hold = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowHold));
        Assert.Equal((int)WalletBalanceSource.Combined, hold.BalanceSource);
        Assert.Equal(600_000m, hold.DepositedAmount);
        Assert.Equal(400_000m, hold.EarnedAmount);
        Assert.Equal(1_000_000m, hold.TokenAmount);

        var fee = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Equal(10_000m, fee.TokenAmount);
        Assert.Equal((int)WalletBalanceSource.Earned, fee.BalanceSource);
        Assert.Null(fee.DepositedAmount);
        Assert.Equal(10_000m, fee.EarnedAmount);
    }

    [Fact]
    public async Task FundEscrow_ExactGCoinMath_Funding200Debits202AndHolds200()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToFullySignedPendingEscrow();
        fixture.Contract.TotalBudget = 200m;
        fixture.Escrows.Entities[0].RequiredAmount = 200m;
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 202m,
            WithdrawableTokens = 0m,
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

        // Funding 200 G-coin: -202 available (200 hold + 2 fee), +200 held. No ÷1000.
        var wallet = fixture.Wallets.Entities[0];
        Assert.Equal(0m, wallet.AvailableTokens);
        Assert.Equal(200m, wallet.HeldTokens);
        Assert.Equal(200m, result.HeldTokens);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal(200m, fixture.Escrows.Entities[0].FundedAmount);

        var hold = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowHold));
        Assert.Equal(200m, hold.TokenAmount);
        Assert.Equal(200m, hold.VndAmount);
        Assert.Equal($"ESCROW-HOLD-{fixture.Escrows.Entities[0].ContractEscrowId:N}", hold.GatewayTransactionCode);

        var fee = Assert.Single(fixture.WalletTransactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Equal(2m, fee.TokenAmount);
        Assert.Equal($"SERVICE-FEE-FUND-{fixture.ContractId:N}", fee.GatewayTransactionCode);

        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task FundEscrow_DuplicateRequestDoesNotDoubleDebit()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToFullySignedPendingEscrow();
        fixture.Contract.TotalBudget = 200m;
        fixture.Escrows.Entities[0].RequiredAmount = 200m;
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 202m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        var handler = new FundContractEscrowCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        var first = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        Assert.Equal((int)ContractStatus.Active, first.ContractStatus);

        var walletTransactionsAfterFirst = fixture.WalletTransactions.Entities.Count;
        var escrowTransactionsAfterFirst = fixture.EscrowTransactions.Entities.Count;

        var second = await handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.Active, second.ContractStatus);
        Assert.Equal(0m, fixture.Wallets.Entities[0].AvailableTokens);
        Assert.Equal(200m, fixture.Wallets.Entities[0].HeldTokens);
        Assert.Equal(walletTransactionsAfterFirst, fixture.WalletTransactions.Entities.Count);
        Assert.Equal(escrowTransactionsAfterFirst, fixture.EscrowTransactions.Entities.Count);
    }

    [Fact]
    public async Task FundEscrow_RejectsWhenCombinedDepositedAndEarnedIsInsufficient()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToFullySignedPendingEscrow();
        fixture.Contract.TotalBudget = 1_000_000m;
        fixture.Escrows.Entities[0].RequiredAmount = 1_000_000m;
        fixture.Wallets.Add(new UserWallet
        {
            UserWalletsId = fixture.WalletId,
            UserId = fixture.ClientUserId,
            AvailableTokens = 600_000m,
            WithdrawableTokens = 400_000m,
            HeldTokens = 0m,
            CreatedAt = fixture.Now
        });

        var handler = new FundContractEscrowCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopNotificationService(),
            new NoopChatRealtimeNotifier());

        // 600,000 deposited + 400,000 earned = 1,000,000, but the 10,000-token fee pushes it over.
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
                CancellationToken.None));

        var wallet = fixture.Wallets.Entities[0];
        Assert.Equal(600_000m, wallet.AvailableTokens);
        Assert.Equal(400_000m, wallet.WithdrawableTokens);
        Assert.Equal(0m, wallet.HeldTokens);
        Assert.Empty(fixture.WalletTransactions.Entities);
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
            AvailableTokens = 1_010m,
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
        Assert.Equal(1_000m, result.RequiredAmountVnd);
        Assert.Equal(1_000m, result.HeldTokens);
        Assert.Equal(0m, fixture.Wallets.Entities[0].AvailableTokens);
        Assert.Equal(1_000m, fixture.Wallets.Entities[0].HeldTokens);
        Assert.Equal((int)ContractEscrowStatus.Funded, fixture.Escrows.Entities[0].Status);
        Assert.Equal(fixture.Contract.TotalBudget, fixture.Escrows.Entities[0].RequiredAmount);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal(2, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Equal(2, fixture.WalletTransactions.Entities.Count);
        Assert.Single(fixture.EscrowTransactions.Entities);
    }

    [Fact]
    public async Task FundEscrow_DoesNotBridgeLegacyJobPostSignature()
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

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new FundContractEscrowCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None));

        var contractDocument = fixture.GetContractDocument();
        var contractSignatures = fixture.EsignSignatures.Entities
            .Where(signature => signature.EsignDocumentsId == contractDocument.EsignDocumentsId)
            .ToList();

        Assert.Contains("both parties sign", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.PartiallySigned, contractDocument.Status);
        Assert.DoesNotContain(contractSignatures, signature => signature.UserId == fixture.ClientUserId);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.FreelancerUserId &&
            signature.SignerRole == (int)ESignerRole.Freelancer);
        Assert.Equal((int)ContractEscrowStatus.PendingFunding, fixture.Escrows.Entities[0].Status);
        Assert.Empty(fixture.WalletTransactions.Entities);
        Assert.Empty(fixture.EscrowTransactions.Entities);
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
            fixture.MediaService,
            fixture.DocumentGenerator);

        await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.ClientUserId,
                new SignContractRequest(SignatureDataUri, 300, 100, true, "1.0-DATN"),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal((int)ESignDocumentStatus.PartiallySigned, fixture.EsignDocuments.Entities[0].Status);
        Assert.Equal(fixture.ClientSignatureUrl, fixture.EsignSignatures.Entities[0].SignatureImageUrl);
        Assert.Equal("esign/signatures", fixture.MediaService.Uploads[0].Folder);
        Assert.Equal("image/png", fixture.MediaService.Uploads[0].ContentType);
        Assert.Equal("1.0-DATN", fixture.EsignSignatures.Entities[0].PolicyVersion);
        Assert.Equal(fixture.Now, fixture.EsignSignatures.Entities[0].PolicyAcceptedAt);
        Assert.Null(fixture.EsignDocuments.Entities[0].FinalizedDocumentContent);
        Assert.Empty(fixture.DeliveryOutboxes.Entities);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest(SignatureDataUri, null, null, true, "1.0-DATN"),
                    null,
                    null),
                CancellationToken.None));

        var result = await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new SignContractRequest(SignatureDataUri, 300, 100, true, "1.0-DATN"),
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
        Assert.Equal(4, fixture.EsignDocuments.Entities[0].FinalizedDocumentContent?.Length);
        Assert.Equal(4L, fixture.EsignDocuments.Entities[0].FinalizedDocumentSizeBytes);
        Assert.EndsWith(".docx", fixture.EsignDocuments.Entities[0].FinalizedDocumentFileName);
        Assert.Equal(64, fixture.EsignDocuments.Entities[0].DocumentHash?.Length);
        Assert.Single(fixture.DocumentGenerator.GenerateCalls);
        var generation = fixture.DocumentGenerator.GenerateCalls[0];
        Assert.NotEqual(
            generation.DocumentHash,
            ContractEsignRenderer.ComputeFinalHash(
                fixture.EsignDocuments.Entities[0],
                generation.ClientSignature with { PolicyVersion = "changed" },
                generation.FreelancerSignature));
        Assert.Equal(2, fixture.DeliveryOutboxes.Entities.Count);
        Assert.All(fixture.DeliveryOutboxes.Entities, delivery =>
        {
            Assert.Null(delivery.ScheduleId);
            Assert.Equal((int)DeliveryChannel.Email, delivery.Channel);
            Assert.Equal((int)DeliveryOutboxStatus.Pending, delivery.Status);
        });
    }

    [Fact]
    public async Task EnsurePendingEscrow_LoadsPersistedWorkItemsBeforeValidation()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new GigbridgeDbContext(options);
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        var contractId = Guid.NewGuid();
        var jobPostId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        context.Set<JobPost>().Add(new JobPost
        {
            JobPostsId = jobPostId,
            ClientProfilesId = Guid.NewGuid(),
            Title = "Persisted work item contract",
            Description = "Regression test for signing from separate requests.",
            Status = 1,
            CreatedAt = now
        });
        context.Set<Contract>().Add(new Contract
        {
            ContractsId = contractId,
            JobPostsId = jobPostId,
            ClientProfilesId = Guid.NewGuid(),
            Title = "Fixed contract",
            TotalBudget = 100m,
            Status = (int)ContractStatus.PendingSignature,
            CreatedAt = now
        });
        context.Set<Milestone>().Add(new Milestone
        {
            MilestonesId = milestoneId,
            ContractsId = contractId,
            Title = "Implementation",
            Amount = 100m,
            SortOrder = 0,
            Status = (int)MilestoneStatus.Pending,
            CreatedAt = now
        });
        context.Set<ContractWorkItem>().Add(new ContractWorkItem
        {
            ContractWorkItemId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            Title = "Build feature",
            Description = "Build and verify the agreed feature.",
            OrderIndex = 0,
            Status = (int)ContractWorkItemStatus.Todo,
            CreatedAt = now
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persistedContract = await context.Set<Contract>()
            .SingleAsync(contract => contract.ContractsId == contractId);

        var escrow = await ContractEscrowReadiness.EnsurePendingEscrowAsync(
            context,
            persistedContract,
            now.AddMinutes(1),
            CancellationToken.None);

        Assert.Equal(contractId, escrow.ContractsId);
        Assert.Equal(100m, escrow.RequiredAmount);
        Assert.Equal((int)ContractStatus.PendingEscrow, persistedContract.Status);
    }

    [Fact]
    public async Task SignContract_FreelancerSignatureDoesNotBridgeLegacyClientSignature()
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        fixture.AddSignedJobPostDocument();
        var mediaService = new FakeMediaService(fixture.FreelancerSignatureUrl);

        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            mediaService,
            fixture.DocumentGenerator);

        var result = await handler.Handle(
            new SignContractCommand(
                fixture.ContractId,
                fixture.FreelancerUserId,
                new SignContractRequest(SignatureDataUri, 300, 100, true, "1.0-DATN"),
                "127.0.0.1",
                "test"),
            CancellationToken.None);

        var contractDocument = fixture.GetContractDocument();
        var contractSignatures = fixture.EsignSignatures.Entities
            .Where(signature => signature.EsignDocumentsId == contractDocument.EsignDocumentsId)
            .ToList();

        Assert.Equal((int)ContractStatus.PendingSignature, fixture.Contract.Status);
        Assert.Equal((int)ContractStatus.PendingSignature, result.Status);
        Assert.Equal((int)ESignDocumentStatus.PartiallySigned, contractDocument.Status);
        Assert.Null(result.EscrowId);
        Assert.DoesNotContain(contractSignatures, signature => signature.UserId == fixture.ClientUserId);
        Assert.Contains(contractSignatures, signature =>
            signature.UserId == fixture.FreelancerUserId &&
            signature.SignerRole == (int)ESignerRole.Freelancer &&
            signature.SignatureImageUrl == fixture.FreelancerSignatureUrl);
        Assert.Equal(1, fixture.Context.Set<JobPost>().Single().Status);
        Assert.Single(mediaService.Uploads);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "0.9")]
    public async Task SignContract_RejectsMissingOrWrongPolicyAcceptance(bool accepted, string? version)
    {
        var fixture = new ContractWorkflowFixture();
        fixture.MoveToPendingSignatureWithDocument();
        var handler = new SignContractCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            new NoopChatRealtimeNotifier(),
            fixture.MediaService,
            fixture.DocumentGenerator);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest(SignatureDataUri, 300, 100, accepted, version),
                    null,
                    null),
                CancellationToken.None));

        Assert.Empty(fixture.MediaService.Uploads);
        Assert.Empty(fixture.EsignSignatures.Entities);
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
            fixture.MediaService,
            fixture.DocumentGenerator);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SignContractCommand(
                    fixture.ContractId,
                    fixture.ClientUserId,
                    new SignContractRequest("not-base64", null, null, true, "1.0-DATN"),
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

        var submitHandler = new SubmitContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(1)),
            new NoopChatRealtimeNotifier());
        await submitHandler.Handle(
            new SubmitContractDetailsCommand(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);
        var confirmHandler = new ConfirmContractDetailsCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now.AddMinutes(2)),
            new NoopChatRealtimeNotifier(),
            fixture.DocumentGenerator);
        await confirmHandler.Handle(
            new ConfirmContractDetailsCommand(fixture.ContractId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(2, fixture.EsignDocuments.Entities.Count);
        Assert.Single(fixture.EsignDocuments.Entities.Where(document =>
            document.Status == (int)ESignDocumentStatus.Voided));
        Assert.Single(fixture.EsignDocuments.Entities.Where(document =>
            document.Status == (int)ESignDocumentStatus.PendingSignatures));
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
                TotalBudget = 1_000m,
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
            DeliveryOutboxes = Context.AddSet<DeliveryOutbox>();
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
        public FakeContractEsignDocumentGenerator DocumentGenerator { get; } = new();
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
        public TestDbSet<DeliveryOutbox> DeliveryOutboxes { get; }

 
        public void ApplyValidDetails()
        {

            var firstMilestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 1",
                Amount = 400m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 0,
                CreatedAt = Now
            };
            firstMilestone.WorkItems.Add(CreateWorkItem(firstMilestone.MilestonesId, "Implementation", 0));
            Milestones.Add(firstMilestone);
            var secondMilestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = ContractId,
                Title = "Milestone 2",
                Amount = 600m,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = 1,
                CreatedAt = Now
            };
            secondMilestone.WorkItems.Add(CreateWorkItem(secondMilestone.MilestonesId, "Verification", 0));
            Milestones.Add(secondMilestone);
        }

        private ContractWorkItem CreateWorkItem(Guid milestoneId, string title, int orderIndex) => new()
        {
            ContractWorkItemId = Guid.NewGuid(),
            MilestonesId = milestoneId,
            Title = title,
            Description = $"Complete {title.ToLowerInvariant()} scope.",
            Deliverables = "Verified project output",
            EstimatedDuration = "1 week",
            OrderIndex = orderIndex,
            Status = (int)ContractWorkItemStatus.Todo,
            CreatedAt = Now,
            UpdatedAt = Now
        };

        public void MoveToPendingSignature()
        {
            ApplyValidDetails();
            Contract.Status = (int)ContractStatus.PendingSignature;
            Escrows.Add(new ContractEscrow
            {
                ContractEscrowId = Guid.NewGuid(),
                ContractsId = ContractId,
                RequiredAmount = 1_000m,
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
