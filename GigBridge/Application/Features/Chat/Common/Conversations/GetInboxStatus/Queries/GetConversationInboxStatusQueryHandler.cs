using Application.Common.Interfaces;
using Application.Common.InternalServices.Realtime.Models;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Conversations.GetInboxStatus.Queries;

public sealed class GetConversationInboxStatusQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetConversationInboxStatusQuery, RealtimeStatusResponse>
{
    public async Task<RealtimeStatusResponse> Handle(GetConversationInboxStatusQuery request, CancellationToken ct)
    {
        var state = await context.Set<UserRealtimeState>().AsNoTracking()
            .TagWith("Chat.InboxStatus")
            .Where(item => item.UserId == request.UserId)
            .Select(item => new RealtimeStatusResponse(item.ConversationRevision, item.ConversationUnreadCount))
            .SingleOrDefaultAsync(ct);
        if (state is not null) return state;
        var unread = await context.Set<ConversationParticipant>().AsNoTracking()
            .Where(item => item.UserId == request.UserId && item.DeletedAt == null)
            .SumAsync(item => (int?)item.UnreadCount, ct) ?? 0;
        return new RealtimeStatusResponse(0, unread);
    }
}
