using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Domain.Entities;
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

    public static void CreditWithdrawable(UserWallet wallet, decimal tokenAmount, DateTime now)
    {
        wallet.AvailableTokens += tokenAmount;
        wallet.WithdrawableTokens += tokenAmount;
        wallet.UpdatedAt = now;
    }

    public static void DebitAvailable(UserWallet wallet, decimal tokenAmount, DateTime now, string errorMessage)
    {
        if (wallet.AvailableTokens < tokenAmount)
        {
            throw new BadRequestException(errorMessage);
        }

        var nonWithdrawableTokens = Math.Max(0m, wallet.AvailableTokens - wallet.WithdrawableTokens);
        var withdrawableDebit = Math.Max(0m, tokenAmount - nonWithdrawableTokens);
        if (wallet.WithdrawableTokens < withdrawableDebit)
        {
            throw new BadRequestException(errorMessage);
        }

        wallet.AvailableTokens -= tokenAmount;
        wallet.WithdrawableTokens -= withdrawableDebit;
        wallet.UpdatedAt = now;
    }
}
