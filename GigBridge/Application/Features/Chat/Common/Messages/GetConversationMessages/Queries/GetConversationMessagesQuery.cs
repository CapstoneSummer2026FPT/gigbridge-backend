using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;

public record GetConversationMessagesQuery(
    Guid ConversationId,
    Guid UserId,
    DateTime? Before,
    int PageSize = 30) : IRequest<IReadOnlyList<ConversationMessageResponse>>;
