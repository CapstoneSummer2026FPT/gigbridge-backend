using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;

namespace Application.Features.Chat.Common.Messages.Send.DTOs;

public record MessageResponse(
    Guid MessageId,
    Guid ConversationId,
    Guid? SenderUserId,
    int MessageType,
    string? Content,
    Guid? ReplyToMessageId,
    string? Metadata,
    string? ClientMessageId,
    DateTime SentAt,
    DateTime? EditedAt,
    bool IsDeleted,
    IReadOnlyList<MessageAttachmentResponse> Attachments);
