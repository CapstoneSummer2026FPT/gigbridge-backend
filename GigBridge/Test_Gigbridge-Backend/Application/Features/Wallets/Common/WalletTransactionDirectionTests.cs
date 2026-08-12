using Application.Features.Wallets.Common;
using Domain.Enums;

namespace Test_Gigbridge_Backend.Application.Features.Wallets.Common;

public sealed class WalletTransactionDirectionTests
{
    [Theory]
    [InlineData(WalletTransactionType.AdminCredit, WalletBalanceSource.Deposited, true)]
    [InlineData(WalletTransactionType.TopUp, WalletBalanceSource.Deposited, true)]
    [InlineData(WalletTransactionType.EscrowRefund, WalletBalanceSource.Combined, true)]
    [InlineData(WalletTransactionType.WithdrawalRefund, WalletBalanceSource.Earned, true)]
    [InlineData(WalletTransactionType.EscrowHold, WalletBalanceSource.Combined, false)]
    [InlineData(WalletTransactionType.WithdrawalLock, WalletBalanceSource.Earned, false)]
    [InlineData(WalletTransactionType.WithdrawalSuccess, WalletBalanceSource.Earned, false)]
    [InlineData(WalletTransactionType.WithdrawalFee, WalletBalanceSource.Earned, false)]
    [InlineData(WalletTransactionType.DisputePenalty, WalletBalanceSource.HeldDeposited, false)]
    [InlineData(WalletTransactionType.Adjustment, WalletBalanceSource.Deposited, false)]
    [InlineData(WalletTransactionType.SubscriptionPurchase, WalletBalanceSource.Deposited, false)]
    [InlineData(WalletTransactionType.PromotionPurchase, WalletBalanceSource.Deposited, false)]
    public void IsCredit_TypeAloneDeterminesDirection_ForEveryTypeExceptEscrowRelease(
        WalletTransactionType type, WalletBalanceSource balanceSource, bool expectedIsCredit)
    {
        Assert.Equal(expectedIsCredit, WalletTransactionDirection.IsCredit((int)type, (int)balanceSource));
    }

    [Theory]
    [InlineData(WalletBalanceSource.Earned, true)]
    [InlineData(WalletBalanceSource.Deposited, false)]
    [InlineData(WalletBalanceSource.HeldDeposited, false)]
    [InlineData(WalletBalanceSource.HeldEarned, false)]
    [InlineData(WalletBalanceSource.Combined, false)]
    [InlineData(WalletBalanceSource.PendingWithdrawal, false)]
    public void IsCredit_EscrowRelease_IsDirectionAmbiguousAndDependsOnBalanceSource(
        WalletBalanceSource balanceSource, bool expectedIsCredit)
    {
        // Release() always stamps the freelancer's own credited row with Earned, and the
        // client's debit-side row with a Held*/Combined source — never plain Earned.
        Assert.Equal(
            expectedIsCredit,
            WalletTransactionDirection.IsCredit((int)WalletTransactionType.EscrowRelease, (int)balanceSource));
    }
}
