namespace Application.Features.Wallets.Common.DTOs;

public sealed record WalletTransactionResponse(
    Guid WalletTransactionId,
    Guid WalletId,
    Guid UserId,
    decimal TokenAmount,
    decimal VndAmount,
    int Type,
    int Status,
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
