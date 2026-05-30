using Application.Features.JobPosts.GetJobPostDetail.DTOs;
using Application.Features.JobPosts.Services;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.GetJobPostDetail.Queries;

public class GetJobPostDetailQueryHandler : IRequestHandler<GetJobPostDetailQuery, JobPostDetailDto>
{
    private readonly IJobPostsService _jobPostsService;

    public GetJobPostDetailQueryHandler(IJobPostsService jobPostsService)
    {
        _jobPostsService = jobPostsService;
    }

    public async Task<JobPostDetailDto> Handle(GetJobPostDetailQuery request, CancellationToken cancellationToken)
    {
        return await _jobPostsService.GetJobPostDetailAsync(request, cancellationToken);
    }
}