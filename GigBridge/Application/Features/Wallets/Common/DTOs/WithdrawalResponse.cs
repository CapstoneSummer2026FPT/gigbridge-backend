namespace Application.Features.Wallets.Common.DTOs;

public sealed record WithdrawalResponse(
    Guid WithdrawalId,
    Guid UserId,
    Guid WalletId,
    Guid? BankAccountId,
    string BankCode,
    string BankName,
    string BankAccountNumberMasked,
    string BankAccountName,
    decimal TokenAmount,
    decimal VndAmount,
    decimal FeeVnd,
    decimal NetVndAmount,
    int Status,
    string Provider,
    string ProviderOrderCode,
    string? ProviderPayoutId,
    string? ProviderTransactionCode,
    string? ProviderRawStatus,
    string? FailureReason,
    string? LastSyncError,
    DateTime CreatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? LastSyncedAt,
    DateTime? CompletedAt)
{
    public static WithdrawalResponse FromEntity(Domain.Entities.WalletWithdrawal withdrawal)
    {
        return new WithdrawalResponse(
            withdrawal.WalletWithdrawalId,
            withdrawal.UserId,
            withdrawal.UserWalletsId,
            withdrawal.BankAccountId,
            withdrawal.BankCode,
            withdrawal.BankName,
            withdrawal.BankAccountNumberMasked,
            withdrawal.BankAccountName,
            withdrawal.TokenAmount,
            withdrawal.VndAmount,
            withdrawal.FeeVnd,
            withdrawal.NetVndAmount,
            withdrawal.Status,
            withdrawal.Provider,
            withdrawal.ProviderOrderCode,
            withdrawal.ProviderPayoutId,
            withdrawal.ProviderTransactionCode,
            withdrawal.ProviderRawStatus,
            withdrawal.FailureReason,
            withdrawal.LastSyncError,
            withdrawal.CreatedAt,
            withdrawal.ProcessingStartedAt,
            withdrawal.LastSyncedAt,
            withdrawal.CompletedAt);
    }
}
