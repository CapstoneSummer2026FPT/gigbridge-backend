using Application.Common.Interfaces.IService;
using Microsoft.AspNetCore.SignalR;
using Project_API.Hubs;

namespace Project_API.Services.Chat;

public class SignalRChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SignalRChatRealtimeNotifier> _logger;

    public SignalRChatRealtimeNotifier(
        IHubContext<ChatHub> hubContext,
        ILogger<SignalRChatRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendConversationEventAsync(
        Guid conversationId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(ChatHub.GetConversationGroupName(conversationId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send chat event {EventName} to conversation {ConversationId}.",
                eventName,
                conversationId);
        }
    }

    public async Task SendUserEventAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group(ChatHub.GetUserGroupName(userId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send chat event {EventName} to user {UserId}.",
                eventName,
                userId);
        }
    }

    public async Task SendUsersEventAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        try
        {
            var groupNames = userIds
                .Distinct()
                .Select(ChatHub.GetUserGroupName)
                .ToList();

            await _hubContext.Clients
                .Groups(groupNames)
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send chat event {EventName} to {UserCount} users.",
                eventName,
                userIds.Count);
        }
    }
}
