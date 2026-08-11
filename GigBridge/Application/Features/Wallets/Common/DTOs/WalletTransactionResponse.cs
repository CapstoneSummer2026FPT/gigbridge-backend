using Application.Features.Wallets.Common;

namespace Application.Features.Wallets.Common.DTOs;

public sealed record WalletTransactionResponse(
    Guid WalletTransactionId,
    Guid WalletId,
    Guid UserId,
    decimal TokenAmount,
    decimal VndAmount,
    int Type,
    int Status,
    int BalanceSource,
    bool IsCredit,
    decimal? DepositedAmount,
    decimal? EarnedAmount,
    string? IdempotencyKey,
    string? GatewayProvider,
    string? GatewayOrderCode,
    string? GatewayTransactionCode,
    Guid? ContractId,
    Guid? ContractEscrowId,
    string? Note,
    DateTime CreatedAt,
    DateTime? CompletedAt)
{
    public static WalletTransactionResponse FromEntity(Domain.Entities.WalletTransaction transaction)
    {
        return new WalletTransactionResponse(
            transaction.WalletTransactionsId,
            transaction.UserWalletsId,
            transaction.UserId,
            transaction.TokenAmount,
            transaction.VndAmount,
            transaction.Type,
            transaction.Status,
            transaction.BalanceSource,
            WalletTransactionDirection.IsCredit(transaction.Type, transaction.BalanceSource),
            transaction.DepositedAmount,
            transaction.EarnedAmount,
            transaction.IdempotencyKey,
            transaction.GatewayProvider,
            transaction.GatewayOrderCode,
            transaction.GatewayTransactionCode,
            transaction.ContractsId,
            transaction.ContractEscrowId,
            transaction.Note,
            transaction.CreatedAt,
            transaction.CompletedAt);
    }
}
