using MediatR;

namespace Application.Features.Chat.Conversations.EnsureParticipant.Queries;

public record EnsureConversationParticipantQuery(
    Guid ConversationId,
    Guid UserId) : IRequest<bool>;
