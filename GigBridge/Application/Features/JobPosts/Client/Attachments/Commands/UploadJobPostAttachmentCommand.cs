using Application.Features.JobPosts.Common.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Client.Attachments.Commands;

public sealed record UploadJobPostAttachmentCommand(
    Guid JobPostId,
    Guid UserId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSize) : IRequest<AttachmentDto>;
