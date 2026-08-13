using Application.Features.Wallets.Common.Models;

namespace Application.Features.Wallets.Common.Interfaces;

public interface IWalletTopUpPaymentService
{
    Task<WalletTopUpPaymentResult> CreatePaymentAsync(
        WalletTopUpPaymentRequest request,
        CancellationToken cancellationToken);

    Task<WalletTopUpCallbackResult> VerifyCallbackAsync(
        WalletTopUpCallbackPayload payload,
        CancellationToken cancellationToken);

    Task<WalletTopUpStatusResult> GetPaymentStatusAsync(
        long orderCode,
        CancellationToken cancellationToken);
}
