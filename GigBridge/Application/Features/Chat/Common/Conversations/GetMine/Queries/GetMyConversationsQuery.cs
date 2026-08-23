using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetMine.Queries;

public record GetMyConversationsQuery(
    Guid UserId,
    ConversationPageCursor? Cursor = null,
    int? Take = null,
    Guid? ContractId = null,
    Guid? DisputeId = null,
    Guid? ProposalId = null,
    Guid? JobPostId = null,
    Guid? ConversationId = null) : IRequest<IReadOnlyList<ConversationSummaryResponse>>;

public sealed record ConversationPageCursor(DateTime SortAt, Guid ConversationId);
