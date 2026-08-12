namespace Application.Features.Chat.Common.Interfaces;

public interface IChatRealtimeNotifier
{
    Task SendConversationEventAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);

    Task SendUserEventAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);

    Task SendUsersEventAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
