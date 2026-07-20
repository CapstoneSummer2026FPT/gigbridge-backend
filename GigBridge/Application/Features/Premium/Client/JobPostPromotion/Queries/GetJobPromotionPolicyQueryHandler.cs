using Application.Common.Interfaces;
using Application.Features.Premium.Client.JobPostPromotion.Common;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using MediatR;

namespace Application.Features.Premium.Client.JobPostPromotion.Queries;

public sealed class GetJobPromotionPolicyQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetJobPromotionPolicyQuery, JobPromotionPolicyDto>
{
    public Task<JobPromotionPolicyDto> Handle(GetJobPromotionPolicyQuery request, CancellationToken cancellationToken) =>
        JobPromotionPolicy.LoadAsync(context, cancellationToken);
}
