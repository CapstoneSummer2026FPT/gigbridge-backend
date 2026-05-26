using Application.DTOs.Admin;
using Application.Features.Admin.JobPosts.Dto;
using MediatR;

namespace Application.Features.Admin.JobPosts.Command;

public sealed record CancelJobPostCommand(
    Guid JobPostId,
    JobStatusRequestDto Request,
    AdminActorDto Actor) : IRequest<JobPostDetailDto>;
