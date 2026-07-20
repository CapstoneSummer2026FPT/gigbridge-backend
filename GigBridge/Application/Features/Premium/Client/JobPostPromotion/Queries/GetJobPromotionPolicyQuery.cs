using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Queries;

public sealed record GetJobPromotionPolicyQuery : IRequest<JobPromotionPolicyDto>;
