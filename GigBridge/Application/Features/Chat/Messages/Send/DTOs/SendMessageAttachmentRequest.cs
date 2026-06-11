namespace Application.Features.Chat.Messages.Send.DTOs;

public record SendMessageAttachmentRequest(
    string FileName,
    string FileUrl,
    string StorageProvider,
    string? StorageObjectKey,
    string MimeType,
    string? FileExtension,
    long FileSizeBytes);
