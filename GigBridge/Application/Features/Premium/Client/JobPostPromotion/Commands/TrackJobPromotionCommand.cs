using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public enum JobPromotionInteractionType { Impression, Click }

public sealed record TrackJobPromotionCommand(Guid PromotionId, JobPromotionInteractionType Type)
    : IRequest<JobPromotionInteractionDto>;
