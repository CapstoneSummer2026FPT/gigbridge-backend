using Application.Features.Premium.Freelancer.Promotions.DTOs;
using MediatR;
namespace Application.Features.Premium.Freelancer.Promotions.Track;
public enum PromotionInteractionType { Impression, Click }
public sealed record TrackPromotionInteractionCommand(Guid PromotionId, string VisitorKey, PromotionInteractionType Type) : IRequest<PromotionInteractionResultDto>;
