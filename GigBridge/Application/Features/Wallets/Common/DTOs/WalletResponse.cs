using Domain.Services.Payments;

namespace Application.Features.Wallets.Common.DTOs;

public sealed record WalletResponse(
    Guid WalletId,
    Guid UserId,
    decimal AvailableTokens,
    decimal HeldTokens,
    decimal PendingWithdrawalTokens,
    decimal AvailableVnd,
    decimal HeldVnd,
    decimal PendingWithdrawalVnd)
{
    public static WalletResponse FromEntity(Domain.Entities.UserWallet wallet)
    {
        return new WalletResponse(
            wallet.UserWalletsId,
            wallet.UserId,
            wallet.AvailableTokens,
            wallet.HeldTokens,
            wallet.PendingWithdrawalTokens,
            TokenWalletRules.ToVnd(wallet.AvailableTokens),
            TokenWalletRules.ToVnd(wallet.HeldTokens),
            TokenWalletRules.ToVnd(wallet.PendingWithdrawalTokens));
    }
}
