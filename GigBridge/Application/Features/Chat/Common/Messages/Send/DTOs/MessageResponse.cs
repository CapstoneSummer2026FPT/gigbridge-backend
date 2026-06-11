namespace Application.Features.Chat.Common.Messages.Send.DTOs;

public record MessageResponse(
    Guid MessageId,
    Guid ConversationId,
    Guid? SenderUserId,
    int MessageType,
    string? Content,
    string? Metadata,
    DateTime SentAt,
    bool IsDeleted);
