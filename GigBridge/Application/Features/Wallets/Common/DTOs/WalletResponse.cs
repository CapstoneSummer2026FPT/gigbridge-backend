using Domain.Services.Payments;

namespace Application.Features.Wallets.Common.DTOs;

/// <summary>
/// Wallet balances with explicit, unambiguous names. The wallet has two independent
/// spendable pools:
/// <list type="bullet">
/// <item><see cref="DepositedGigCoin"/> — purchased/deposited, spendable but NOT withdrawable.</item>
/// <item><see cref="WithdrawableGigCoin"/> — earned from completed work, spendable AND withdrawable.</item>
/// </list>
/// <see cref="TotalSpendableGigCoin"/> is the sum of both spendable pools and must never be
/// treated as a withdrawal maximum — withdrawals may only use <see cref="WithdrawableGigCoin"/>.
/// </summary>
public sealed record WalletResponse(
    Guid WalletId,
    Guid UserId,
    decimal DepositedGigCoin,
    decimal WithdrawableGigCoin,
    decimal HeldGigCoin,
    decimal PendingWithdrawalGigCoin,
    decimal TotalSpendableGigCoin,
    decimal DepositedGigCoinVnd,
    decimal WithdrawableGigCoinVnd,
    decimal HeldGigCoinVnd,
    decimal PendingWithdrawalGigCoinVnd,
    decimal TotalSpendableGigCoinVnd)
{
    public static WalletResponse FromEntity(Domain.Entities.UserWallet wallet)
    {
        var totalSpendable = wallet.AvailableTokens + wallet.WithdrawableTokens;
        return new WalletResponse(
            wallet.UserWalletsId,
            wallet.UserId,
            wallet.AvailableTokens,
            wallet.WithdrawableTokens,
            wallet.HeldTokens,
            wallet.PendingWithdrawalTokens,
            totalSpendable,
            TokenWalletRules.ToVnd(wallet.AvailableTokens),
            TokenWalletRules.ToVnd(wallet.WithdrawableTokens),
            TokenWalletRules.ToVnd(wallet.HeldTokens),
            TokenWalletRules.ToVnd(wallet.PendingWithdrawalTokens),
            TokenWalletRules.ToVnd(totalSpendable));
    }
}
