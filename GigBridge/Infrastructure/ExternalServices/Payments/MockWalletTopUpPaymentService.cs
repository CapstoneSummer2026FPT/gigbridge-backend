using Application.Common.Interfaces.IService;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Payments;

public sealed class MockWalletTopUpPaymentService : IWalletTopUpPaymentService
{
    private const string GatewayProvider = "PayOS";
    private readonly PayOsOptions _options;

    public MockWalletTopUpPaymentService(IOptions<PayOsOptions> options)
    {
        _options = options.Value;
    }

    public Task<WalletTopUpPaymentResult> CreatePaymentAsync(
        WalletTopUpPaymentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var paymentLinkId = $"mock-{request.OrderCode}";
        var returnUrl = request.ReturnUrl ?? _options.ReturnUrl ?? "http://localhost:5173/wallet/deposit?result=success";
        var cancelUrl = request.CancelUrl ?? _options.CancelUrl ?? "http://localhost:5173/wallet/deposit?result=cancel";
        var frontendOrigin = GetOrigin(returnUrl) ?? GetOrigin(cancelUrl) ?? "http://localhost:5173";
        var checkoutUrl =
            $"{frontendOrigin}/wallet/mock-checkout" +
            $"?orderCode={request.OrderCode}" +
            $"&amount={Uri.EscapeDataString(request.AmountVnd.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))}" +
            $"&paymentLinkId={Uri.EscapeDataString(paymentLinkId)}" +
            $"&returnUrl={Uri.EscapeDataString(returnUrl)}" +
            $"&cancelUrl={Uri.EscapeDataString(cancelUrl)}";

        return Task.FromResult(new WalletTopUpPaymentResult(
            GatewayProvider,
            request.OrderCode.ToString(),
            paymentLinkId,
            checkoutUrl));
    }

    public Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
        WalletTopUpCallbackPayload payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isVerified = payload.Signature == "mock-signature" ||
            PayOsSignatureVerifier.IsValid(
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

    private static string? GetOrigin(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;
    }
}
