using Application.Features.Chat.Common.Conversations.GetUnreadCount.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Conversations.GetUnreadCount.Queries;

public sealed record GetConversationUnreadCountQuery(Guid UserId)
    : IRequest<ConversationUnreadCountResponse>;
