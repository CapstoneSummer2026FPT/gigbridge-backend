using System;

namespace Application.Features.JobPosts.Common.DTOs;

public record AttachmentDto(Guid JobPostAttachmentsId, string FileUrl, string FileName);