using Application.Features.Chat.Common.Conversations.GetUnreadCount.Queries;
using Domain.Entities;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Chat.Common.Conversations;

public sealed class GetConversationUnreadCountQueryHandlerTests
{
    [Fact]
    public async Task Handle_SumsOnlyActiveParticipantsForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var context = new InMemoryApplicationDbContext();
        context.AddSet(
            Participant(userId, unreadCount: 2),
            Participant(userId, unreadCount: 3),
            Participant(userId, unreadCount: 50, deletedAt: DateTime.UtcNow),
            Participant(userId, unreadCount: 75, leftAt: DateTime.UtcNow),
            Participant(Guid.NewGuid(), unreadCount: 100));
        var handler = new GetConversationUnreadCountQueryHandler(context);

        var result = await handler.Handle(
            new GetConversationUnreadCountQuery(userId),
            CancellationToken.None);

        Assert.Equal(5, result.UnreadCount);
    }

    [Fact]
    public async Task Handle_ReturnsZeroWhenUserHasNoConversations()
    {
        var context = new InMemoryApplicationDbContext();
        context.AddSet<ConversationParticipant>();
        var handler = new GetConversationUnreadCountQueryHandler(context);

        var result = await handler.Handle(
            new GetConversationUnreadCountQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(0, result.UnreadCount);
    }

    private static ConversationParticipant Participant(
        Guid userId,
        int unreadCount,
        DateTime? deletedAt = null,
        DateTime? leftAt = null)
    {
        return new ConversationParticipant
        {
            ConversationParticipantId = Guid.NewGuid(),
            ConversationsId = Guid.NewGuid(),
            UserId = userId,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            UnreadCount = unreadCount,
            DeletedAt = deletedAt,
            LeftAt = leftAt
        };
    }
}
