namespace Application.Common.Interfaces.IService;

public interface IPayoutProvider
{
    string ProviderName { get; }

    Task<PayoutProviderResult> CreatePayoutAsync(
        PayoutCreateRequest request,
        CancellationToken cancellationToken);

    Task<PayoutProviderResult> GetPayoutStatusAsync(
        PayoutStatusRequest request,
        CancellationToken cancellationToken);

    Task<PayoutWebhookVerificationResult> VerifyWebhookAsync(
        PayoutWebhookVerificationRequest request,
        CancellationToken cancellationToken);
}

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
    string BankCode,
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
    string? FailureReason,
    string? RawPayload);

public sealed record PayoutWebhookVerificationRequest(
    string RawPayload,
    string? Signature);

public sealed record PayoutWebhookVerificationResult(
    bool IsVerified,
    string? EventId,
    string? ProviderOrderCode,
    string? ProviderPayoutId,
    PayoutProviderOutcome Outcome,
    string? ProviderTransactionCode,
    string? RawStatus,
    string? FailureReason,
    string? RawPayload);
