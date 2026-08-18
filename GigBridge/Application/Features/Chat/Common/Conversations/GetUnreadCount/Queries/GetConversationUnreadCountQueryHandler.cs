using Application.Common.Interfaces;
using Application.Features.Chat.Common.Conversations.GetUnreadCount.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Conversations.GetUnreadCount.Queries;

public sealed class GetConversationUnreadCountQueryHandler
    : IRequestHandler<GetConversationUnreadCountQuery, ConversationUnreadCountResponse>
{
    private readonly IApplicationDbContext _context;

    public GetConversationUnreadCountQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ConversationUnreadCountResponse> Handle(
        GetConversationUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        var unreadCount = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(participant =>
                participant.UserId == request.UserId &&
                participant.DeletedAt == null)
            .SumAsync(participant => (int?)participant.UnreadCount, cancellationToken) ?? 0;

        return new ConversationUnreadCountResponse(unreadCount);
    }
}
