namespace Application.Common.Interfaces.IService;

public interface IWalletTopUpPaymentService
{
    Task<WalletTopUpPaymentResult> CreatePaymentAsync(
        WalletTopUpPaymentRequest request,
        CancellationToken cancellationToken);

    Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
        WalletTopUpCallbackPayload payload,
        CancellationToken cancellationToken);
}

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
    string? FailureReason);

public sealed record WalletTopUpCallbackResult(
    long? OrderCode,
    bool IsSucceeded,
    string? GatewayTransactionCode,
    decimal? AmountVnd,
    string? FailureReason);
