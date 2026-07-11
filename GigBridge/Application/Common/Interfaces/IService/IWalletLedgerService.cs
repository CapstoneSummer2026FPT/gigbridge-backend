using Domain.Entities;
using Domain.Enums;

namespace Application.Common.Interfaces.IService;

public interface IWalletLedgerService
{
    Task<WalletTransaction> DebitAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken);

    Task<WalletTransaction> CreditAsync(
        Guid userId,
        decimal tokenAmount,
        WalletTransactionType type,
        string idempotencyKey,
        string? metadata,
        CancellationToken cancellationToken);
}
