using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Services;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.GetMyJobPosts.Queries;

public class GetMyJobPostsQueryHandler : IRequestHandler<GetMyJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IJobPostsService _jobPostsService;

    public GetMyJobPostsQueryHandler(IJobPostsService jobPostsService)
    {
        _jobPostsService = jobPostsService;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetMyJobPostsQuery request, CancellationToken cancellationToken)
    {
        return await _jobPostsService.GetMyJobPostsAsync(request, cancellationToken);
    }
}