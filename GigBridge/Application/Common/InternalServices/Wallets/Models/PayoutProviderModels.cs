namespace Application.Common.InternalServices.Wallets.Models;
public enum PayoutProviderOutcome
{
    Accepted = 0,
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    SyncRequired = 4
}

public sealed record PayoutCreateRequest(
    Guid WithdrawalId,
    string ProviderOrderCode,
    decimal AmountVnd,
    string BankBin,
    string AccountNumber,
    string AccountName,
    string Description,
    string IdempotencyKey);

public sealed record PayoutStatusRequest(
    Guid WithdrawalId,
    string ProviderOrderCode,
    string? ProviderPayoutId);

public sealed record PayoutProviderResult(
    PayoutProviderOutcome Outcome,
    string? ProviderPayoutId,
    string? ProviderTransactionCode,
    string? RawStatus,
    string? FailureReason);

public sealed record PayoutProviderAvailability(
    bool IsAvailable,
    decimal? BalanceVnd,
    string? ErrorCode,
    string? SafeMessage);
