using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.GetAvailableJobPosts.Queries;

public class GetAvailableJobPostsQueryHandler : IRequestHandler<GetAvailableJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IJobPostsService _jobPostsService;

    public GetAvailableJobPostsQueryHandler(IJobPostsService jobPostsService)
    {
        _jobPostsService = jobPostsService;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetAvailableJobPostsQuery request, CancellationToken cancellationToken)
    {
        return await _jobPostsService.GetAvailableJobPostsAsync(request, cancellationToken);
    }
}