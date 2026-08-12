using Application.Features.Chat.Common.Interfaces;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class NoopChatRealtimeNotifier : IChatRealtimeNotifier
{
    public Task SendConversationEventAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendUserEventAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendUsersEventAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
