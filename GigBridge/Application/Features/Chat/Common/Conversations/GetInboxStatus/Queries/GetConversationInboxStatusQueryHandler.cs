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
        var revision = await context.Set<UserRealtimeState>().AsNoTracking()
            .TagWith("Chat.InboxStatus")
            .Where(item => item.UserId == request.UserId)
            .Select(item => (int?)item.ConversationRevision)
            .SingleOrDefaultAsync(ct);

        // ConversationParticipant is the source of truth. UserRealtimeState is a
        // delivery cursor/cache and can temporarily drift when several nodes update
        // the same user's conversations concurrently. Never expose that drift as a
        // persistent unread badge.
        var unread = await context.Set<ConversationParticipant>().AsNoTracking()
            .Where(item =>
                item.UserId == request.UserId &&
                item.LeftAt == null &&
                item.DeletedAt == null)
            .SumAsync(item => (int?)item.UnreadCount, ct) ?? 0;

        return new RealtimeStatusResponse(revision ?? 0, unread);
    }
}
