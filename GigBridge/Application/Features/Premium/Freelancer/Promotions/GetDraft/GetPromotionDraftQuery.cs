using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;

namespace Application.Features.Premium.Freelancer.Promotions.GetDraft;
public sealed record GetPromotionDraftQuery(Guid UserId) : IRequest<PromotionDraftDto>;
