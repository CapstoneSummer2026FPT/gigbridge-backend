using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetMine.Queries;

public sealed record GetConversationSummaryPageQuery(
    Guid UserId,
    string? Cursor,
    int PageSize,
    Guid? ContractId,
    Guid? DisputeId,
    Guid? ProposalId,
    Guid? JobPostId) : IRequest<ConversationSummaryPageResponse>;
