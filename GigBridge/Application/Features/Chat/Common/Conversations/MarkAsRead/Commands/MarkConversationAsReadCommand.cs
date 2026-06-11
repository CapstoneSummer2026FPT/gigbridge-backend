using MediatR;

namespace Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;

public record MarkConversationAsReadCommand(
    Guid ConversationId,
    Guid MessageId,
    Guid UserId) : IRequest<bool>;
