using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.GetManager;
public sealed record GetPromotionManagerQuery(Guid UserId) : IRequest<PromotionManagerDto>;
