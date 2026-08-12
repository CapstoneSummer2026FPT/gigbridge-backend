using Application.Features.Contracts.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Wallets;

/// <summary>
/// Locks in the exact G-coin math for the contract economy after the unit-boundary fix:
/// contract, milestone and escrow amounts are G-coin directly (never divided by 1000),
/// and the 1% service fee is charged once on the G-coin amount, at funding (client) or
/// job acceptance (freelancer) only. These are the regression cases from the audit:
/// funding 200 G-coin debits 202 (200 + 2 fee) and holds 200; the 80% release of a 200
/// G-coin milestone credits the full 160 to the freelancer with no further fee; funding
/// 500 debits 502; refunds restore the G-coin amount directly.
/// </summary>
public sealed class GigCoinExactMathTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FundingQuote_200GCoin_Requires200Plus2FeeTotal202()
    {
        var quote = ContractEscrowFundingQuote.Calculate(200m);

        Assert.Equal(200m, quote.RequiredTokens);
        Assert.Equal(0.01m, quote.FundingFeeRate);
        Assert.Equal(2m, quote.FundingFeeTokens);
        Assert.Equal(2_000m, quote.FundingFeeVnd);
        Assert.Equal(202m, quote.TotalDebitTokens);
    }

    [Fact]
    public void FundingQuote_500GCoin_Requires500Plus5FeeTotal505()
    {
        var quote = ContractEscrowFundingQuote.Calculate(500m);

        Assert.Equal(500m, quote.RequiredTokens);
        Assert.Equal(5m, quote.FundingFeeTokens);
        Assert.Equal(5_000m, quote.FundingFeeVnd);
        Assert.Equal(505m, quote.TotalDebitTokens);
    }

    [Fact]
    public void FundingQuote_FractionalGCoin_FeeRoundsToFourDecimals()
    {
        var quote = ContractEscrowFundingQuote.Calculate(123.456m);

        Assert.Equal(123.456m, quote.RequiredTokens);
        Assert.Equal(1.2346m, quote.FundingFeeTokens);
        Assert.Equal(124.6906m, quote.TotalDebitTokens);
    }

    [Fact]
    public void Release_EightyPercentOf200CoinMilestone_CreditsFull160NoFee()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var revenue = context.AddSet<PlatformRevenueEvent>();
        var contractId = Guid.NewGuid();
        var escrowId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            HeldTokens = 160m
        };
        var freelancer = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = escrowId,
            ContractsId = contractId,
            DepositedTokens = 100m,
            EarnedTokens = 60m
        };

        // 80% cap of a 200 G-coin milestone = 160 G-coin released, credited in full.
        var result = ContractEscrowWalletWorkflow.Release(
            context, client, freelancer, escrow, contractId, milestoneId,
            160m, "ESCROW-RELEASE-EXACT", "InternalTokenWallet", "Milestone early withdrawal", Now);

        Assert.Equal(160m, result.GrossTokens);
        Assert.Equal(0m, result.FeeTokens);
        Assert.Equal(160m, result.NetTokens);
        Assert.Equal(0m, client.HeldTokens);
        Assert.Equal(160m, freelancer.WithdrawableTokens);
        Assert.Equal(0m, escrow.DepositedTokens);
        Assert.Equal(0m, escrow.EarnedTokens);

        var clientRelease = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowRelease &&
            transaction.UserId == client.UserId));
        Assert.Equal(160m, clientRelease.TokenAmount);
        Assert.Equal((int)WalletBalanceSource.Combined, clientRelease.BalanceSource);

        var freelancerRelease = Assert.Single(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.EscrowRelease &&
            transaction.UserId == freelancer.UserId));
        Assert.Equal(160m, freelancerRelease.TokenAmount);
        Assert.Equal((int)WalletBalanceSource.Earned, freelancerRelease.BalanceSource);

        Assert.Empty(transactions.Entities.Where(transaction =>
            transaction.Type == (int)WalletTransactionType.Adjustment));
        Assert.Empty(revenue.Entities);
    }

    [Fact]
    public void Release_FractionalAmount_CreditsFullGrossNoFee()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<WalletTransaction>();
        var client = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid(), HeldTokens = 160.5m };
        var freelancer = new UserWallet { UserWalletsId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = Guid.NewGuid(),
            DepositedTokens = 160.5m,
            EarnedTokens = 0m
        };

        var result = ContractEscrowWalletWorkflow.Release(
            context, client, freelancer, escrow, escrow.ContractsId, null,
            160.5m, "release-fractional", "InternalTokenWallet", "Release", Now);

        Assert.Equal(160.5m, result.GrossTokens);
        Assert.Equal(0m, result.FeeTokens);
        Assert.Equal(160.5m, result.NetTokens);
        Assert.Equal(0m, client.HeldTokens);
    }

    [Fact]
    public void Refund_200GCoin_RestoresDepositedPoolDirectly()
    {
        var context = new InMemoryApplicationDbContext();
        var transactions = context.AddSet<WalletTransaction>();
        var contractId = Guid.NewGuid();
        var client = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 200m
        };
        var escrow = new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contractId,
            DepositedTokens = 200m,
            EarnedTokens = 0m
        };

        var refunded = ContractEscrowWalletWorkflow.Refund(
            context, client, escrow, contractId, null,
            200m, "REFUND-200-EXACT", "InternalTokenWallet", "Contract amendment decrease", Now);

        Assert.Equal(200m, refunded);
        Assert.Equal(0m, client.HeldTokens);
        Assert.Equal(200m, client.AvailableTokens);
        Assert.Equal(0m, client.WithdrawableTokens);
        var refund = Assert.Single(transactions.Entities);
        Assert.Equal((int)WalletTransactionType.EscrowRefund, refund.Type);
        Assert.Equal(200m, refund.TokenAmount);
        Assert.Equal((int)WalletBalanceSource.HeldDeposited, refund.BalanceSource);
    }
}
