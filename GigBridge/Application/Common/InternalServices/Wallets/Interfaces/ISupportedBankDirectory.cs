using Application.Common.InternalServices.Wallets.Models;

namespace Application.Common.InternalServices.Wallets.Interfaces;
public interface ISupportedBankDirectory
{
    Task<IReadOnlyList<SupportedBank>> GetBanksAsync(CancellationToken cancellationToken);
}
