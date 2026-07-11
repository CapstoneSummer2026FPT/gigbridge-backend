using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.Common;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.GetPackages;

public sealed class GetPromotionPackagesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPromotionPackagesQuery, IReadOnlyList<PromotionPackageDto>>
{
    public async Task<IReadOnlyList<PromotionPackageDto>> Handle(
        GetPromotionPackagesQuery request, CancellationToken cancellationToken) =>
        (await PromotionPackages.LoadAsync(context, cancellationToken))
            .Where(item => item.IsActive).ToList();
}
