using Domain.Enums.Chat;
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
    MessageResponse? LastMessage,
    Guid? OtherParticipantId,
    string? OtherParticipantName,
    string? OtherParticipantAvatar,
    int? OtherParticipantRole,
    string? OtherParticipantCompany,
    string? OtherParticipantRoleTitle,
    Guid? LastOfferId,
    decimal? LastOfferPrice,
    int? LastOfferStatus,
    decimal? JobBudgetMin,
    decimal? JobBudgetMax,
    string? JobCurrency,
    string? JobCategoryName,
    decimal? ProposalBudget,
    string? ProposalDuration,
    int? JobStatus,
    int? JobVisibility,
    bool CanNegotiate);

