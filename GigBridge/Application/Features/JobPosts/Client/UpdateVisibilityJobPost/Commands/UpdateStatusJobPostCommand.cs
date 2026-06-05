using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;

public record UpdateStatusJobPostCommand(
    Guid JobPostId,
    Guid UserId,
    UpdateStatusJobPostRequest Request
) : IRequest<bool>;