using Application.Features.Chat.Conversations.GetMine.DTOs;
using MediatR;

namespace Application.Features.Chat.Conversations.GetMine.Queries;

public record GetMyConversationsQuery(Guid UserId) : IRequest<IReadOnlyList<ConversationSummaryResponse>>;
