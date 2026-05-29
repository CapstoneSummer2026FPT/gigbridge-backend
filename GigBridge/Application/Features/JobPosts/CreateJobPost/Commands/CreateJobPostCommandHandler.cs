using Application.Features.JobPosts.Services;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.JobPosts.CreateJobPost.Commands;

public class CreateJobPostCommandHandler : IRequestHandler<CreateJobPostCommand, Guid>
{
    private readonly IJobPostsService _jobPostsService;

    public CreateJobPostCommandHandler(IJobPostsService jobPostsService)
    {
        _jobPostsService = jobPostsService;
    }

    public async Task<Guid> Handle(CreateJobPostCommand command, CancellationToken cancellationToken)
    {
        return await _jobPostsService.CreateJobPostAsync(command, cancellationToken);
    }
}