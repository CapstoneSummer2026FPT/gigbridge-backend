using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Commands;

public sealed record UpdateJobPromotionPolicyCommand(
    Guid AdminUserId,
    UpdateJobPromotionPolicyRequest Request) : IRequest<JobPromotionPolicyDto>;
