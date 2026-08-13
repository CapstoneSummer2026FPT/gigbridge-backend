using Application.Features.Wallets.Common.Models;

namespace Application.Features.Wallets.Common.Interfaces;

public interface ISupportedBankDirectory
{
    Task<IReadOnlyList<SupportedBank>> GetBanksAsync(CancellationToken cancellationToken);
}
