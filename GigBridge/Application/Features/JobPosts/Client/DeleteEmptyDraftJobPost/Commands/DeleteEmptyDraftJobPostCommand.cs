using MediatR;

namespace Application.Features.JobPosts.Client.DeleteEmptyDraftJobPost.Commands;

public sealed record DeleteEmptyDraftJobPostCommand(Guid JobPostId, Guid UserId)
    : IRequest<bool>;
