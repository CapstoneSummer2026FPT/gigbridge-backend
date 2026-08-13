namespace Application.Features.Contracts.Common.GetWorkspaceFiles.DTOs;

public record WorkspaceFileResponse(
    Guid MessageAttachmentId,
    Guid? MessageId,
    string FileName,
    string FileUrl,
    string MimeType,
    string? FileExtension,
    long FileSizeBytes,
    DateTime UploadedAt,
    Guid? UploaderUserId,
    string? UploaderName,
    string? UploaderAvatar,
    int SourceType = 0,
    string? ExternalUrl = null,
    string? Note = null,
    int? Version = null,
    string Context = "message",
    Guid? MilestoneId = null,
    string? MilestoneTitle = null);
