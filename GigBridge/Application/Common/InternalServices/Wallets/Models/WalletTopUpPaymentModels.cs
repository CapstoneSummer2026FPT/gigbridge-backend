namespace Application.Common.InternalServices.Wallets.Models;
public sealed record WalletTopUpPaymentRequest(
    Guid WalletTransactionId,
    Guid UserId,
    long OrderCode,
    decimal TokenAmount,
    decimal AmountVnd,
    string Description,
    string? ReturnUrl,
    string? CancelUrl);

public sealed record WalletTopUpPaymentResult(
    string GatewayProvider,
    string GatewayOrderCode,
    string? GatewayTransactionCode,
    string? CheckoutUrl);

public sealed record WalletTopUpCallbackPayload(
    long? OrderCode,
    bool IsSucceeded,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    string? FailureReason,
    string? Signature,
    IReadOnlyDictionary<string, string?> SignatureData);

public sealed record WalletTopUpCallbackResult(
    bool IsVerified,
    long? OrderCode,
    bool IsSucceeded,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    string? FailureReason);

public sealed record WalletTopUpStatusResult(
    long? OrderCode,
    string? Status,
    bool IsSucceeded,
    bool IsCancelled,
    bool IsFailed,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    string? FailureReason);
