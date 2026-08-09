using Application.Features.Chat.Common.Schedules;
using Domain.Enums;

namespace Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;

public record ConversationMessageResponse(
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
    IReadOnlyList<MessageAttachmentResponse> Attachments,
    ScheduleEventResponse? Schedule = null,
    string? SenderName = null,
    string? SenderAvatar = null,
    int? SenderRole = null,
    DisputeMessageRecipient? DisputeRecipient = null);
