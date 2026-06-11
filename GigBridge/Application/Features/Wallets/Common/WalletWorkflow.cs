using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common;

internal static class WalletWorkflow
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
            HeldTokens = 0m,
            CreatedAt = now
        };

        context.Set<UserWallet>().Add(wallet);
        return wallet;
    }
}
