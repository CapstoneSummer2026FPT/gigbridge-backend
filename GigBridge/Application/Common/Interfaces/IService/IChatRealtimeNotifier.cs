namespace Application.Common.Interfaces.IService;

public interface IChatRealtimeNotifier
{
    Task SendConversationEventAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
