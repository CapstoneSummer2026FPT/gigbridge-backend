namespace Application.Features.Chat.Messages.GetConversationMessages.DTOs;

public record MessageAttachmentResponse(
    Guid MessageAttachmentId,
    string FileName,
    string FileUrl,
    string StorageProvider,
    string? StorageObjectKey,
    string MimeType,
    string? FileExtension,
    long FileSizeBytes,
    DateTime CreatedAt);
