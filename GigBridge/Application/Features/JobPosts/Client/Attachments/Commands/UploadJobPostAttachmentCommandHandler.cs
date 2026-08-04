using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.Attachments.Commands;

public sealed class UploadJobPostAttachmentCommandHandler(
    IApplicationDbContext context,
    IMediaService mediaService,
    IDateTimeService dateTimeService)
    : IRequestHandler<UploadJobPostAttachmentCommand, AttachmentDto>
{
    private const int MaximumAttachmentCount = 5;
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedExtensions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"]
        };

    public async Task<AttachmentDto> Handle(
        UploadJobPostAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileSize <= 0 || request.FileSize > MaximumFileSize)
            throw new BadRequestException("Job post image must be between 1 byte and 5 MB.");

        var safeFileName = Path.GetFileName(request.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !AllowedExtensions.TryGetValue(request.ContentType, out var extensions) ||
            !extensions.Contains(Path.GetExtension(safeFileName), StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Job post attachments must be JPEG, PNG, or WebP images.");
        }

        var clientProfileId = await context.Set<ClientProfile>()
            .Where(profile => profile.UserId == request.UserId)
            .Select(profile => (Guid?)profile.ClientProfilesId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Client profile does not exist.");

        var jobPost = await context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.JobPostsId == request.JobPostId &&
                        item.ClientProfilesId == clientProfileId,
                cancellationToken)
            ?? throw new NotFoundException("Job post does not exist or you do not have permission to update it.");

        if (jobPost.Visibility == 3)
            throw new BadRequestException("This job post has been locked by an admin and cannot be updated.");

        var attachmentCount = await context.Set<JobPostAttachment>()
            .CountAsync(item => item.JobPostsId == request.JobPostId, cancellationToken);
        if (attachmentCount >= MaximumAttachmentCount)
            throw new BadRequestException($"A job post may contain at most {MaximumAttachmentCount} images.");

        MemoryStream? bufferedContent = null;
        var uploadContent = request.Content;
        if (!uploadContent.CanSeek)
        {
            bufferedContent = new MemoryStream();
            await uploadContent.CopyToAsync(bufferedContent, cancellationToken);
            bufferedContent.Position = 0;
            uploadContent = bufferedContent;
        }

        try
        {
            await EnsureImageSignatureAsync(uploadContent, request.ContentType, cancellationToken);
            uploadContent.Position = 0;

            var fileUrl = await mediaService.UploadFileAsync(
                uploadContent,
                safeFileName,
                request.ContentType,
                $"job-posts/{request.JobPostId}/attachments",
                cancellationToken);

            var attachment = new JobPostAttachment
            {
                JobPostAttachmentsId = Guid.NewGuid(),
                JobPostsId = request.JobPostId,
                FileName = safeFileName,
                FileUrl = fileUrl,
                FileSize = request.FileSize,
                CreatedAt = dateTimeService.UtcNow
            };
            context.Set<JobPostAttachment>().Add(attachment);
            await context.SaveChangesAsync(cancellationToken);

            return new AttachmentDto(
                attachment.JobPostAttachmentsId,
                attachment.FileUrl,
                attachment.FileName);
        }
        finally
        {
            if (bufferedContent is not null)
                await bufferedContent.DisposeAsync();
        }
    }

    private static async Task EnsureImageSignatureAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await content.ReadAsync(
                header.AsMemory(bytesRead, header.Length - bytesRead),
                cancellationToken);
            if (read == 0) break;
            bytesRead += read;
        }

        var valid = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytesRead >= 3 &&
                            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => bytesRead >= 8 &&
                           header.AsSpan(0, 8).SequenceEqual(
                               new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytesRead >= 12 &&
                            header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                            header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!valid)
            throw new BadRequestException("The uploaded file content is not a valid image.");
    }
}
