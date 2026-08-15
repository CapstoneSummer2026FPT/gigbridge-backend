using Application.Common.InternalServices.Premium.Models;
using Application.Features.Premium.Common;

namespace Application.Common.InternalServices.Premium.Interfaces;
public interface IPremiumAccessService
{
    Task<bool> IsPremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsPremiumClientAsync(Guid userId, CancellationToken cancellationToken);
    Task<PremiumBenefitsDto> GetPremiumBenefitsAsync(Guid userId, CancellationToken cancellationToken);
    Task RequirePremiumFreelancerAsync(Guid userId, CancellationToken cancellationToken);
    Task RequirePremiumClientAsync(Guid userId, CancellationToken cancellationToken);
}
