using Application.Features.Admin.JobPosts.Command;
using Application.Features.Admin.JobPosts.Dto;
using Infrastructure.Services.Admin.Interfaces;
using MediatR;

namespace Infrastructure.Services.Admin.Handlers.JobPosts;

public sealed class CancelJobPostHandler : IRequestHandler<CancelJobPostCommand, JobPostDetailDto>
{
    private readonly IAdminJobPostService _jobPostService;

    public CancelJobPostHandler(IAdminJobPostService jobPostService)
    {
        _jobPostService = jobPostService;
    }

    public Task<JobPostDetailDto> Handle(CancelJobPostCommand request, CancellationToken cancellationToken) =>
        _jobPostService.CancelAsync(request.JobPostId, request.Request, request.Actor, cancellationToken);
}

