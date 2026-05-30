using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.GetMyAppliedJobPosts.Queries;

public class GetMyAppliedJobPostsQueryHandler : IRequestHandler<GetMyAppliedJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IJobPostsService _jobPostsService;

    public GetMyAppliedJobPostsQueryHandler(IJobPostsService jobPostsService)
    {
        _jobPostsService = jobPostsService;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetMyAppliedJobPostsQuery request, CancellationToken cancellationToken)
    {
        return await _jobPostsService.GetMyAppliedJobPostsAsync(request, cancellationToken);
    }
}