using Application.Common.InternalServices.Wallets.Models;

namespace Application.Common.InternalServices.Wallets.Interfaces;
public interface IPayoutProvider
{
    string ProviderName { get; }

    Task<PayoutProviderResult> CreatePayoutAsync(
        PayoutCreateRequest request,
        CancellationToken cancellationToken);

    Task<PayoutProviderResult> GetPayoutStatusAsync(
        PayoutStatusRequest request,
        CancellationToken cancellationToken);

    Task<PayoutProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken);

}
