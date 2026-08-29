using Application.Features.Chat.Common.Conversations.GetInboxStatus.Queries;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Chat.Common.Conversations;

public sealed class GetConversationInboxStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenRealtimeCacheDrifts_ReturnsAuthoritativeUnreadCount()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(new UserRealtimeState
        {
            UserId = userId,
            ConversationRevision = 7,
            ConversationUnreadCount = 3,
            UpdatedAt = DateTime.UtcNow
        });
        context.AddSet(
            Participant(userId, unreadCount: 0),
            Participant(userId, unreadCount: 9, leftAt: DateTime.UtcNow),
            Participant(Guid.NewGuid(), unreadCount: 20));
        var handler = new GetConversationInboxStatusQueryHandler(context);

        var result = await handler.Handle(
            new GetConversationInboxStatusQuery(userId),
            CancellationToken.None);

        Assert.Equal(7, result.Revision);
        Assert.Equal(0, result.UnreadCount);
    }

    [Fact]
    public async Task Handle_WithoutRealtimeState_ReturnsAuthoritativeUnreadCountAtRevisionZero()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet<UserRealtimeState>();
        context.AddSet(Participant(userId, unreadCount: 2));
        var handler = new GetConversationInboxStatusQueryHandler(context);

        var result = await handler.Handle(
            new GetConversationInboxStatusQuery(userId),
            CancellationToken.None);

        Assert.Equal(0, result.Revision);
        Assert.Equal(2, result.UnreadCount);
    }

    private static ConversationParticipant Participant(
        Guid userId,
        int unreadCount,
        DateTime? leftAt = null)
    {
        return new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = Guid.NewGuid(),
            UserId = userId,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            UnreadCount = unreadCount,
            LeftAt = leftAt
        };
    }
}
