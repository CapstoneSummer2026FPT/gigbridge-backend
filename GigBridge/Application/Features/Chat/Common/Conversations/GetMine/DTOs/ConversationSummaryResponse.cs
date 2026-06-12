using Application.Features.Chat.Common.Messages.Send.DTOs;

namespace Application.Features.Chat.Common.Conversations.GetMine.DTOs;

public record ConversationSummaryResponse(
    Guid ConversationId,
    int ConversationType,
    string? Title,
    Guid? JobPostId,
    Guid? ProposalId,
    Guid? ContractId,
    Guid? DisputeId,
    int Status,
    int UnreadCount,
    DateTime CreatedAt,
    DateTime? LastMessageAt,
    MessageResponse? LastMessage);
