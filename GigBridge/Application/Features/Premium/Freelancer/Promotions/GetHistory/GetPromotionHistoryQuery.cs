using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.GetHistory;

public sealed record GetPromotionHistoryQuery(Guid UserId) : IRequest<IReadOnlyList<PromotionDto>>;
