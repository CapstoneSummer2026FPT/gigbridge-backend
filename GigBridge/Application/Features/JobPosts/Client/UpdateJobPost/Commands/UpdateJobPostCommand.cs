using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.UpdateJobPost.Commands;

public record UpdateJobPostCommand(
    Guid JobPostId,
    Guid UserId,
    UpdateJobPostRequest Request
) : IRequest<bool>;