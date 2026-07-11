using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.GetPackages;

public sealed record GetPromotionPackagesQuery : IRequest<IReadOnlyList<PromotionPackageDto>>;
