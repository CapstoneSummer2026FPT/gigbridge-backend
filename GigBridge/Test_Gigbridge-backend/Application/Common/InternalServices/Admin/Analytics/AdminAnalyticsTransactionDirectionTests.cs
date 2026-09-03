using Application.Common.InternalServices.Admin.Analytics.Services;
using Domain.Enums.Wallets;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Admin.Analytics;

/// <summary>
/// An escrow release writes one wallet row per party with the same Type, Status and amount,
/// distinguishable only by BalanceSource. Admin reporting used to count both, so every release
/// showed up twice in the type breakdown, the count series and the CSV export, and both rows
/// carried the same "Transfer" label.
/// </summary>
public sealed class AdminAnalyticsTransactionDirectionTests
{
    [Fact]
    public void An_escrow_release_is_aggregated_once_from_the_credit_leg()
    {
        var release = (int)WalletTransactionType.EscrowRelease;

        Assert.True(AdminAnalyticsService.CountsTowardTransactionAggregates(
            release, (int)WalletBalanceSource.Earned));
    }

    [Theory]
    [InlineData(WalletBalanceSource.HeldDeposited)]
    [InlineData(WalletBalanceSource.HeldEarned)]
    [InlineData(WalletBalanceSource.Combined)]
    public void The_payers_leg_of_an_escrow_release_is_not_aggregated_again(WalletBalanceSource source)
    {
        var release = (int)WalletTransactionType.EscrowRelease;

        Assert.False(AdminAnalyticsService.CountsTowardTransactionAggregates(release, (int)source));
    }

    [Theory]
    [InlineData(WalletTransactionType.TopUp)]
    [InlineData(WalletTransactionType.EscrowHold)]
    [InlineData(WalletTransactionType.EscrowRefund)]
    [InlineData(WalletTransactionType.WithdrawalSuccess)]
    [InlineData(WalletTransactionType.Adjustment)]
    public void Single_legged_types_are_always_aggregated(WalletTransactionType type)
    {
        // Only EscrowRelease is dual-direction; nothing else may be filtered out of the totals.
        Assert.True(AdminAnalyticsService.CountsTowardTransactionAggregates(
            (int)type, (int)WalletBalanceSource.Deposited));
        Assert.True(AdminAnalyticsService.CountsTowardTransactionAggregates(
            (int)type, (int)WalletBalanceSource.Earned));
    }

    [Fact]
    public void The_two_legs_of_a_release_are_labelled_differently()
    {
        var release = (int)WalletTransactionType.EscrowRelease;

        var payee = AdminAnalyticsService.Direction(release, (int)WalletBalanceSource.Earned);
        var payer = AdminAnalyticsService.Direction(release, (int)WalletBalanceSource.HeldDeposited);

        Assert.NotEqual(payer, payee);
    }

    [Fact]
    public void Non_release_labels_are_unchanged_by_the_balance_source()
    {
        Assert.Equal(
            "Credit",
            AdminAnalyticsService.Direction((int)WalletTransactionType.TopUp, (int)WalletBalanceSource.Deposited));
        Assert.Equal(
            "Hold",
            AdminAnalyticsService.Direction((int)WalletTransactionType.EscrowHold, (int)WalletBalanceSource.HeldDeposited));
        Assert.Equal(
            "Debit",
            AdminAnalyticsService.Direction((int)WalletTransactionType.WithdrawalSuccess, (int)WalletBalanceSource.PendingWithdrawal));
    }
}
