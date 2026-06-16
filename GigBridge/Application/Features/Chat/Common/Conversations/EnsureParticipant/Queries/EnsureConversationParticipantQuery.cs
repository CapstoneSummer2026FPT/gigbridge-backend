using MediatR;

namespace Application.Features.Chat.Common.Conversations.EnsureParticipant.Queries;

public record EnsureConversationParticipantQuery(
    Guid ConversationId,
    Guid UserId) : IRequest<bool>;
