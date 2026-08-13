using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Wallets;
using Domain.Services.Payments;

namespace Test_Gigbridge_Backend.Application.Features.Wallets.Common;

/// <summary>
/// Verifies the wallet DTOs expose the two GigCoin pools under unambiguous names so the
/// frontend never mistakes the combined spendable balance for a withdrawal maximum.
/// </summary>
public sealed class WalletDtoTests
{
    [Fact]
    public void WalletResponse_ExposesSeparateDepositedAndWithdrawablePools()
    {
        var wallet = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 120.5m,
            WithdrawableTokens = 37.25m,
            HeldTokens = 10m,
            PendingWithdrawalTokens = 5m
        };

        var response = WalletResponse.FromEntity(wallet);

        Assert.Equal(wallet.UserWalletsId, response.WalletId);
        Assert.Equal(wallet.UserId, response.UserId);
        Assert.Equal(120.5m, response.DepositedGigCoin);
        Assert.Equal(37.25m, response.WithdrawableGigCoin);
        Assert.Equal(10m, response.HeldGigCoin);
        Assert.Equal(5m, response.PendingWithdrawalGigCoin);
        Assert.Equal(157.75m, response.TotalSpendableGigCoin);

        Assert.Equal(TokenWalletRules.ToVnd(120.5m), response.DepositedGigCoinVnd);
        Assert.Equal(TokenWalletRules.ToVnd(37.25m), response.WithdrawableGigCoinVnd);
        Assert.Equal(TokenWalletRules.ToVnd(10m), response.HeldGigCoinVnd);
        Assert.Equal(TokenWalletRules.ToVnd(5m), response.PendingWithdrawalGigCoinVnd);
        Assert.Equal(TokenWalletRules.ToVnd(157.75m), response.TotalSpendableGigCoinVnd);
    }

    [Fact]
    public void WalletResponse_TotalSpendableIsDepositedPlusWithdrawableNotWithdrawalLimit()
    {
        var wallet = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AvailableTokens = 200m,
            WithdrawableTokens = 0m,
            HeldTokens = 0m,
            PendingWithdrawalTokens = 0m
        };

        var response = WalletResponse.FromEntity(wallet);

        // A client with only deposited GigCoin has a large spendable balance but nothing
        // eligible for withdrawal.
        Assert.Equal(200m, response.TotalSpendableGigCoin);
        Assert.Equal(0m, response.WithdrawableGigCoin);
    }

    [Fact]
    public void WalletTransactionResponse_MapsBalanceSourceAndSplitAmounts()
    {
        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenAmount = 100m,
            VndAmount = 100_000m,
            BalanceSource = (int)WalletBalanceSource.Combined,
            DepositedAmount = 60m,
            EarnedAmount = 40m,
            Type = (int)WalletTransactionType.EscrowHold,
            Status = (int)WalletTransactionStatus.Succeeded,
            IdempotencyKey = "key-1",
            CreatedAt = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 12, 9, 0, 1, DateTimeKind.Utc)
        };

        var response = WalletTransactionResponse.FromEntity(transaction);

        Assert.Equal(transaction.WalletTransactionsId, response.WalletTransactionId);
        Assert.Equal(transaction.UserWalletsId, response.WalletId);
        Assert.Equal(transaction.UserId, response.UserId);
        Assert.Equal(100m, response.TokenAmount);
        Assert.Equal(100_000m, response.VndAmount);
        Assert.Equal((int)WalletBalanceSource.Combined, response.BalanceSource);
        Assert.Equal(60m, response.DepositedAmount);
        Assert.Equal(40m, response.EarnedAmount);
        Assert.Equal((int)WalletTransactionType.EscrowHold, response.Type);
        Assert.Equal((int)WalletTransactionStatus.Succeeded, response.Status);
        Assert.Equal("key-1", response.IdempotencyKey);
        // EscrowHold always debits (locks) the client's balance regardless of BalanceSource.
        Assert.False(response.IsCredit);
    }

    [Fact]
    public void WalletTransactionResponse_SingleSourceTransactionLeavesOtherSplitNull()
    {
        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenAmount = 25m,
            VndAmount = 25_000m,
            BalanceSource = (int)WalletBalanceSource.Earned,
            DepositedAmount = null,
            EarnedAmount = 25m,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            CreatedAt = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 12, 9, 0, 1, DateTimeKind.Utc)
        };

        var response = WalletTransactionResponse.FromEntity(transaction);

        Assert.Equal((int)WalletBalanceSource.Earned, response.BalanceSource);
        Assert.Null(response.DepositedAmount);
        Assert.Equal(25m, response.EarnedAmount);
        // EscrowRelease with an Earned source is the freelancer's own credited row.
        Assert.True(response.IsCredit);
    }

    [Fact]
    public void WalletTransactionResponse_EscrowReleaseWithHeldSource_IsTheClientsDebitNotACredit()
    {
        // The client's side of the same EscrowRelease event never carries a plain Earned
        // source (it's Combined/HeldEarned/HeldDeposited) — this is what distinguishes it
        // from the freelancer's credited row above, without needing a new schema field.
        var transaction = new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenAmount = 25m,
            VndAmount = 25_000m,
            BalanceSource = (int)WalletBalanceSource.HeldDeposited,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            CreatedAt = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc)
        };

        var response = WalletTransactionResponse.FromEntity(transaction);

        Assert.False(response.IsCredit);
    }
}
