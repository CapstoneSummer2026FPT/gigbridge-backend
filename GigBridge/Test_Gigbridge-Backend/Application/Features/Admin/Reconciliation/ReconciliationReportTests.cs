using Application.Features.Admin.Reconciliation.Common.DTOs;
using Application.Features.Admin.Reconciliation.Queries;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Admin.Reconciliation;

public sealed class ReconciliationReportTests
{
    private static readonly DateTime Now = new(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Report_FlagsDeflatedEscrowCompositionButNotHealthyEscrow()
    {
        var context = new InMemoryApplicationDbContext();
        var contractA = Contract("Healthy Escrow", 200m, new[] { 200m });
        var escrowA = Escrow(contractA, "EscrowA", 200m, 200m, 200m, 0m);
        var contractB = Contract("Deflated Escrow", 200m, new[] { 200m });
        var escrowB = Escrow(contractB, "EscrowB", 200m, 200m, 0.2m, 0m);
        context.AddSet(escrowA, escrowB);
        context.AddSet(contractA, contractB);

        var report = await BuildAsync(context);

        // Healthy escrow: funded exactly, composition invariant holds -> no drift.
        Assert.DoesNotContain(report.EscrowCompositionDrift, item => item.ContractEscrowId == escrowA.ContractEscrowId);

        // Deflated escrow: 200 G-coin budget, only 0.2 G-coin actually held -> drift -199.8.
        var deflated = Assert.Single(report.EscrowCompositionDrift);
        Assert.Equal(escrowB.ContractEscrowId, deflated.ContractEscrowId);
        Assert.Equal(-199.8m, deflated.CompositionDelta);
        Assert.True(deflated.LikelyDeflated);
    }

    [Fact]
    public async Task Report_DoesNotListFullyFundedEscrowInFundingDrift()
    {
        var context = new InMemoryApplicationDbContext();
        var contract = Contract("Funded", 200m, new[] { 200m });
        var escrow = Escrow(contract, "Escrow", 200m, 200m, 200m, 0m);
        context.AddSet(escrow);
        context.AddSet(contract);

        var report = await BuildAsync(context);

        Assert.Empty(report.EscrowFundingDrift);
        Assert.Empty(report.EscrowCompositionDrift);
    }

    [Fact]
    public async Task Report_FlagsMilestonePlanWhereBudgetDoesNotMatchMilestoneSum()
    {
        var context = new InMemoryApplicationDbContext();
        var healthy = Contract("Healthy Plan", 200m, new[] { 200m });
        var broken = Contract("Broken Plan", 200m, new[] { 100m, 80m });
        context.AddSet(healthy, broken);

        var report = await BuildAsync(context);

        var item = Assert.Single(report.MilestonePlanDrift);
        Assert.Equal(broken.ContractsId, item.ContractsId);
        Assert.Equal(200m, item.TotalBudget);
        Assert.Equal(180m, item.MilestoneTotal);
        Assert.Equal(20m, item.Delta);
    }

    [Fact]
    public async Task Report_ReconstructsHealthyPoolsFromLedgerWithNoDrift()
    {
        var context = new InMemoryApplicationDbContext();
        var contract = Contract("Healthy", 200m, new[] { 200m });
        var escrow = Escrow(contract, "Escrow", 200m, 200m, 200m, 0m);
        context.AddSet(contract);
        context.AddSet(escrow);

        var client = Wallet("client", 0m, 0m, 200m, 0m);
        var freelancer = Wallet("freelancer", 0m, 160m, 0m, 0m);
        context.AddSet(client, freelancer);

        // Client funded a 200-coin escrow (202 available -> 200 held + 2 fee);
        // freelancer received a 160-coin release (1.6 fee is already netted, ledger-only).
        context.AddSet(
            Ledger(client, WalletTransactionType.TopUp, 202m, 202m, null, "topup-1"),
            Ledger(client, WalletTransactionType.EscrowHold, 200m, 200m, null, "ESCROW-HOLD-escrow"),
            Ledger(client, WalletTransactionType.Adjustment, 2m, 2m, null, "SERVICE-FEE-FUND-contract"),
            Ledger(freelancer, WalletTransactionType.EscrowRelease, 160m, null, 160m, "ESCROW-RELEASE-x"),
            Ledger(freelancer, WalletTransactionType.Adjustment, 1.6m, null, 1.6m, "SERVICE-FEE-RELEASE-x"));

        var report = await BuildAsync(context);

        Assert.Empty(report.WalletPoolDrift);
    }

    [Fact]
    public async Task Report_FlagsWalletPoolDriftAndCountsUnclassifiedAdjustments()
    {
        var context = new InMemoryApplicationDbContext();
        var wallet = Wallet("drifted", 10m, 0m, 40m, 0m);
        context.AddSet(wallet);
        context.AddSet(
            // Ledger says 50 held + an unclassified admin debit, but the wallet disagrees.
            Ledger(wallet, WalletTransactionType.EscrowHold, 50m, 50m, null, "ESCROW-HOLD-y"),
            Ledger(wallet, WalletTransactionType.Adjustment, 10m, 10m, null, "ADMIN-MANUAL-FIX-1"));

        var report = await BuildAsync(context);

        var item = Assert.Single(report.WalletPoolDrift);
        Assert.Equal(wallet.UserId, item.UserId);
        Assert.Equal(10m, item.AvailableTokens);
        Assert.Equal(-60m, item.ExpectedAvailable);
        Assert.Equal(40m, item.HeldTokens);
        Assert.Equal(50m, item.ExpectedHeld);
        Assert.Equal(1, item.UnclassifiedAdjustmentCount);
    }

    [Fact]
    public async Task Report_SummarizesCountsAndNeverWrites()
    {
        var context = new InMemoryApplicationDbContext();
        var contract = Contract("Healthy", 200m, new[] { 200m });
        var escrow = Escrow(contract, "Escrow", 200m, 200m, 200m, 0m);
        context.AddSet(contract);
        context.AddSet(escrow);
        var client = Wallet("client", 0m, 0m, 200m, 0m);
        context.AddSet(client);
        context.AddSet(Ledger(client, WalletTransactionType.EscrowHold, 200m, 200m, null, "ESCROW-HOLD-z"));

        var report = await BuildAsync(context);

        Assert.Equal(1, report.Summary.EscrowCount);
        Assert.Equal(1, report.Summary.ContractCount);
        Assert.Equal(1, report.Summary.WalletCount);
        Assert.Equal(0, report.Summary.CompositionDriftCount);
        Assert.Equal(0, context.SaveChangesCount);
    }

    private static async Task<EscrowReconciliationReport> BuildAsync(InMemoryApplicationDbContext context)
    {
        var handler = new GetEscrowReconciliationReportQueryHandler(context);
        return await handler.Handle(new GetEscrowReconciliationReportQuery(), CancellationToken.None);
    }

    private static Contract Contract(string title, decimal budget, params decimal[] milestoneAmounts)
    {
        var contract = new Contract
        {
            ContractsId = Guid.NewGuid(),
            Title = title,
            TotalBudget = budget,
            JobPostsId = Guid.NewGuid(),
            ClientProfilesId = Guid.NewGuid(),
            CreatedAt = Now
        };
        contract.Milestones = milestoneAmounts
            .Select((amount, index) => new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                Title = $"Milestone {index + 1}",
                Amount = amount,
                Status = (int)MilestoneStatus.Pending,
                SortOrder = index,
                CreatedAt = Now,
                Contracts = contract
            })
            .ToList();
        return contract;
    }

    private static ContractEscrow Escrow(Contract contract, string id, decimal required, decimal funded, decimal held, decimal released)
    {
        var escrowId = Guid.NewGuid();
        return new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contract.ContractsId,
            RequiredAmount = required,
            FundedAmount = funded,
            DepositedTokens = held,
            EarnedTokens = 0m,
            ReleasedAmount = released,
            Status = (int)ContractEscrowStatus.Funded,
            CreatedAt = Now,
            Contract = contract
        };
    }

    private static UserWallet Wallet(string user, decimal available, decimal withdrawable, decimal held, decimal pending)
    {
        var walletId = Guid.NewGuid();
        return new UserWallet
        {
            UserWalletsId = walletId,
            UserId = Guid.NewGuid(),
            AvailableTokens = available,
            WithdrawableTokens = withdrawable,
            HeldTokens = held,
            PendingWithdrawalTokens = pending,
            CreatedAt = Now
        };
    }

    private static WalletTransaction Ledger(
        UserWallet wallet,
        WalletTransactionType type,
        decimal tokenAmount,
        decimal? deposited,
        decimal? earned,
        string key) => new()
    {
        WalletTransactionsId = Guid.NewGuid(),
        UserWalletsId = wallet.UserWalletsId,
        UserId = wallet.UserId,
        TokenAmount = tokenAmount,
        VndAmount = tokenAmount,
        BalanceSource = (int)(earned > 0m ? WalletBalanceSource.Earned : WalletBalanceSource.Deposited),
        DepositedAmount = deposited,
        EarnedAmount = earned,
        Type = (int)type,
        Status = (int)WalletTransactionStatus.Succeeded,
        IdempotencyKey = key,
        CreatedAt = Now,
        CompletedAt = Now
    };
}
