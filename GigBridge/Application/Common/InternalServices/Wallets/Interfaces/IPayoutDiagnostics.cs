using Application.Common.InternalServices.Wallets.Models;

namespace Application.Common.InternalServices.Wallets.Interfaces;

/// <summary>
/// Describes how the payout client is wired on this node. Kept separate from
/// <see cref="IPayoutProvider"/> because it reports configuration and network identity rather than
/// payout state, and only the admin diagnostic endpoint needs it.
/// </summary>
public interface IPayoutDiagnostics
{
    Task<PayoutProviderDiagnostics> DescribeAsync(CancellationToken cancellationToken);
}
