using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.GetCurrent;

public sealed record GetCurrentPromotionQuery(Guid UserId) : IRequest<PromotionDto?>;
