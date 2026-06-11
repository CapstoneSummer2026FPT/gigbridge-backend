using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Conversations.EnsureParticipant.Queries;

public class EnsureConversationParticipantQueryHandler
    : IRequestHandler<EnsureConversationParticipantQuery, bool>
{
    private readonly IApplicationDbContext _context;

    public EnsureConversationParticipantQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> Handle(
        EnsureConversationParticipantQuery request,
        CancellationToken cancellationToken)
    {
        return _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .AnyAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == request.UserId &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null,
                cancellationToken);
    }
}
