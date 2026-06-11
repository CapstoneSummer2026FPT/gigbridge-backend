using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetMine.Queries;

public record GetMyConversationsQuery(Guid UserId) : IRequest<IReadOnlyList<ConversationSummaryResponse>>;
