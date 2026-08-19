using Application.Common.InternalServices.Chat.Interfaces;

namespace Test_Gigbridge_Backend.TestSupport;

internal sealed class CapturingChatRealtimeNotifier : IChatRealtimeNotifier
{
    public List<CapturedConversationEvent> ConversationEvents { get; } = new();

    public List<CapturedUserEvent> UserEvents { get; } = new();

    public List<CapturedUsersEvent> UsersEvents { get; } = new();

    public Task SendConversationEventAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        ConversationEvents.Add(new CapturedConversationEvent(conversationId, eventName, payload));
        return Task.CompletedTask;
    }

    public Task SendUserEventAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        UserEvents.Add(new CapturedUserEvent(userId, eventName, payload));
        return Task.CompletedTask;
    }

    public Task SendUsersEventAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        UsersEvents.Add(new CapturedUsersEvent(userIds.ToArray(), eventName, payload));
        return Task.CompletedTask;
    }
}

internal sealed record CapturedConversationEvent(
    Guid ConversationId,
    string EventName,
    object Payload);

internal sealed record CapturedUserEvent(
    Guid UserId,
    string EventName,
    object Payload);

internal sealed record CapturedUsersEvent(
    IReadOnlyCollection<Guid> UserIds,
    string EventName,
    object Payload);
