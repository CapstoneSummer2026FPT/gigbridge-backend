using Application.DTOs.Admin;
using MediatR;

namespace Application.Features.Admin.JobPosts.Cancel;

public sealed record CancelJobPostCommand(
    Guid JobPostId,
    JobStatusRequestDto Request,
    AdminActorDto Actor) : IRequest<JobPostDetailDto>;
