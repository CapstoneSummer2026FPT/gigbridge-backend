using Application.Features.Chat.Messages.GetConversationMessages.DTOs;
using MediatR;

namespace Application.Features.Chat.Messages.GetConversationMessages.Queries;

public record GetConversationMessagesQuery(
    Guid ConversationId,
    Guid UserId,
    DateTime? Before,
    int PageSize = 30) : IRequest<IReadOnlyList<ConversationMessageResponse>>;
