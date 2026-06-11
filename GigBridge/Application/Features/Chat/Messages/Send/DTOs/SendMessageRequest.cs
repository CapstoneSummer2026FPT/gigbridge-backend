namespace Application.Features.Chat.Messages.Send.DTOs;

public record SendMessageRequest(
    Guid ConversationId,
    string ClientMessageId,
    string? Content,
    Guid? ReplyToMessageId,
    IReadOnlyCollection<SendMessageAttachmentRequest> Attachments);
