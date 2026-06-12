using Application.Common.Interfaces.IService;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Payments;

public sealed class PayOsWalletTopUpPaymentService : IWalletTopUpPaymentService
{
    private const string GatewayProvider = "PayOS";
    private readonly IPayOsPaymentLinkClient _paymentLinkClient;
    private readonly PayOsOptions _options;

    public PayOsWalletTopUpPaymentService(
        IPayOsPaymentLinkClient paymentLinkClient,
        IOptions<PayOsOptions> options)
    {
        _paymentLinkClient = paymentLinkClient;
        _options = options.Value;
    }

    public async Task<WalletTopUpPaymentResult> CreatePaymentAsync(
        WalletTopUpPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentLinkClient.CreateAsync(
                new PayOsPaymentLinkRequest(
                    request.OrderCode,
                    Convert.ToInt32(request.AmountVnd),
                    ShortDescription(request.OrderCode),
                    request.ReturnUrl ?? _options.ReturnUrl ?? "https://gigbridge.local/wallet/top-up/success",
                    request.CancelUrl ?? _options.CancelUrl ?? "https://gigbridge.local/wallet/top-up/cancel"),
                cancellationToken);

            return new WalletTopUpPaymentResult(
                GatewayProvider,
                request.OrderCode.ToString(),
                result.PaymentLinkId,
                result.CheckoutUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("Unable to create PayOS wallet top-up request.", ex);
        }
    }

    public Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
        WalletTopUpCallbackPayload payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isVerified = PayOsSignatureVerifier.IsValid(
            payload.SignatureData,
            payload.Signature,
            _options.ChecksumKey);

        return Task.FromResult(new WalletTopUpCallbackResult(
            isVerified,
            payload.OrderCode,
            payload.IsSucceeded,
            payload.GatewayTransactionCode,
            payload.AmountVnd,
            payload.FailureReason));
    }

    private static string ShortDescription(long orderCode)
    {
        return $"GB tokens {orderCode % 100000000}";
    }
}
