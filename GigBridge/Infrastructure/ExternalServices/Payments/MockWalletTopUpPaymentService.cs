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

        var checkoutUrl = $"https://payos.mock/gigbridge/top-ups/{request.OrderCode}";

        return Task.FromResult(new WalletTopUpPaymentResult(
            GatewayProvider,
            request.OrderCode.ToString(),
            $"mock-{request.OrderCode}",
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
}
