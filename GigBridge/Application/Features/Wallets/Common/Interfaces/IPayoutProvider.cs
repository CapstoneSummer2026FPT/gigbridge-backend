using Application.Features.Wallets.Common.Models;

namespace Application.Features.Wallets.Common.Interfaces;

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
