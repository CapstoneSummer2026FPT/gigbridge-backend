namespace Application.Features.Chat.Common.Messages.Send.DTOs;

public record SendMessageAttachmentRequest(
    string FileName,
    string FileUrl,
    string StorageProvider,
    string? StorageObjectKey,
    string MimeType,
    string? FileExtension,
    long FileSizeBytes);
