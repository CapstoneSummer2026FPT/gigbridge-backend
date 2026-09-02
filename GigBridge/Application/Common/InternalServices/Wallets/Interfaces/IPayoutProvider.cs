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

    /// <summary>
    /// Reports whether payouts can currently be sent, and the payout account balance.
    /// Results are cached briefly by the implementation; pass <paramref name="bypassCache"/>
    /// to force a live call, which is what the admin diagnostic endpoint needs right after a
    /// credential or IP-whitelist change.
    /// </summary>
    Task<PayoutProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken,
        bool bypassCache = false);

}
