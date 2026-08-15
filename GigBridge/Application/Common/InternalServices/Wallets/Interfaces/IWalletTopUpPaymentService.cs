using Application.Common.InternalServices.Wallets.Models;

namespace Application.Common.InternalServices.Wallets.Interfaces;
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
