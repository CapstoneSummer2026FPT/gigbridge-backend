using Application.Common.Interfaces.IService;

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
}
