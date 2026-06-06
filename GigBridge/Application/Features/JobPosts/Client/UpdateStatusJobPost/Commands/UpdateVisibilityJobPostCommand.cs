using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;

public record UpdateVisibilityJobPostCommand(
    Guid JobPostId,
    Guid UserId,
    UpdateVisibilityJobPostRequest Request
) : IRequest<bool>;