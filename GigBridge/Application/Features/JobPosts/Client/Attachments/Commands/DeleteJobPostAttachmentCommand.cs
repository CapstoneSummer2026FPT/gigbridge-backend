using MediatR;

namespace Application.Features.JobPosts.Client.Attachments.Commands;

public sealed record DeleteJobPostAttachmentCommand(
    Guid JobPostId,
    Guid AttachmentId,
    Guid UserId) : IRequest<bool>;
