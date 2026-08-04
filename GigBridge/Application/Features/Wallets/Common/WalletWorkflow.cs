using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Domain.Entities;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common;

public static class WalletWorkflow
{
    public static async Task<UserWallet> GetOrCreateWalletAsync(
        IApplicationDbContext context,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new UserWallet
        {
            UserWalletsId = Guid.NewGuid(),
            UserId = userId,
            AvailableTokens = 0m,
            WithdrawableTokens = 0m,
            HeldTokens = 0m,
            PendingWithdrawalTokens = 0m,
            CreatedAt = now
        };

        context.Set<UserWallet>().Add(wallet);
        return wallet;
    }

    /// <summary>
    /// Credits the earned (withdrawable) GigCoin pool only. Job earnings, escrow
    /// releases and approved-milestone payouts are always classified as earned and
    /// never touch the deposited (non-withdrawable) pool.
    /// </summary>
    public static void CreditWithdrawable(UserWallet wallet, decimal tokenAmount, DateTime now)
    {
        wallet.WithdrawableTokens += tokenAmount;
        wallet.UpdatedAt = now;
    }

    /// <summary>
    /// Spends from the combined GigCoin balance using the deposited-first rule:
    /// the deposited balance is consumed first, then the earned balance. Rejects the
    /// operation when the combined balances are insufficient. Returns the exact
    /// deposited/earned split so callers can record the transaction balance source.
    /// </summary>
    public static BalanceUsage DebitAvailable(
        UserWallet wallet,
        decimal tokenAmount,
        DateTime now,
        string errorMessage)
    {
        if (!WalletSpendingService.CanSpend(wallet.AvailableTokens, wallet.WithdrawableTokens, tokenAmount))
        {
            throw new BadRequestException(errorMessage);
        }

        var usage = WalletSpendingService.CalculateBalanceUsage(
            wallet.AvailableTokens,
            wallet.WithdrawableTokens,
            tokenAmount)!.Value;

        wallet.AvailableTokens -= usage.DepositedAmount;
        wallet.WithdrawableTokens -= usage.EarnedAmount;
        wallet.UpdatedAt = now;
        return usage;
    }
}
