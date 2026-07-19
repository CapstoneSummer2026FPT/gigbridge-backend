using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed record PromoteJobPostCommand(
    Guid UserId,
    Guid JobPostId,
    PromoteJobPostRequest Request) : IRequest<JobPostPromotionDto>;
