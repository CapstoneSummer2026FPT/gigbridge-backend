using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.Feed;
public sealed record GetPromotionFeedQuery(int Limit) : IRequest<IReadOnlyList<PublicPromotionCardDto>>;
