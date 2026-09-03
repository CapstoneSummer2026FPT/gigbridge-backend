using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Payments.PayOs;

/// <summary>
/// Surfaces a broken payout channel on /health instead of leaving it to be discovered by a
/// freelancer whose withdrawal never arrives. Reports Degraded rather than Unhealthy on purpose:
/// the node still serves every other request correctly, and Nginx must not drop it from the
/// upstream pool over a payout provider fault.
/// </summary>
internal sealed class PayoutProviderHealthCheck : IHealthCheck
{
    private readonly IPayoutProvider _payoutProvider;
    private readonly WalletWithdrawalOptions _options;

    public PayoutProviderHealthCheck(
        IPayoutProvider payoutProvider,
        IOptions<WalletWithdrawalOptions> options)
    {
        _payoutProvider = payoutProvider;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Bank withdrawals are disabled.");
        }

        var availability = await _payoutProvider.CheckAvailabilityAsync(cancellationToken);
        if (availability.IsAvailable)
        {
            return HealthCheckResult.Healthy(
                $"{_payoutProvider.ProviderName} payout is available.",
                new Dictionary<string, object> { ["balanceVnd"] = availability.BalanceVnd ?? 0m });
        }

        return HealthCheckResult.Degraded(
            $"{_payoutProvider.ProviderName} payout is unavailable: {availability.SafeMessage}",
            data: new Dictionary<string, object>
            {
                ["errorCode"] = availability.ErrorCode ?? "UNKNOWN"
            });
    }
}
