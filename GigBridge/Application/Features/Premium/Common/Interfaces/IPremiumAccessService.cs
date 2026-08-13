using Application.Features.Premium.Common;

namespace Application.Features.Premium.Common.Interfaces;

public interface IPremiumAccessService
{
    Task<bool> IsPremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsPremiumClientAsync(Guid userId, CancellationToken cancellationToken);
    Task<PremiumBenefitsDto> GetPremiumBenefitsAsync(Guid userId, CancellationToken cancellationToken);
    Task RequirePremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken);
    Task RequirePremiumClientAsync(Guid userId, CancellationToken cancellationToken);
}
