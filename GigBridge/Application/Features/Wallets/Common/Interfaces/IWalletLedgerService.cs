using Domain.Entities;
using Domain.Enums.Wallets;

namespace Application.Features.Wallets.Common.Interfaces;

public interface IWalletLedgerService
{
    Task<WalletTransaction> DebitAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken);
}
