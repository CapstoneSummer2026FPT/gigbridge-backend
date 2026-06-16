namespace Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;

public record ConversationMessageResponse(
    Guid MessageId,
    Guid ConversationId,
    Guid? SenderUserId,
    int MessageType,
    string? Content,
    Guid? ReplyToMessageId,
    string? Metadata,
    DateTime SentAt,
    DateTime? EditedAt,
    bool IsDeleted,
    IReadOnlyList<MessageAttachmentResponse> Attachments);
