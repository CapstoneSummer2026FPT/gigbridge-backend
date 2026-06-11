using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Application.Features.Chat.Conversations.EnsureParticipant.Queries;
using Application.Features.Chat.Conversations.MarkAsRead.Commands;
using MediatR;

namespace Project_API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;

    public ChatHub(IMediator mediator)
    {
        _mediator = mediator;
    }

    public static string GetConversationGroupName(Guid conversationId)
    {
        return $"conversation:{conversationId}";
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await EnsureParticipant(conversationId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
    }

    public Task LeaveConversation(Guid conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
    }

    public async Task Typing(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await EnsureParticipant(conversationId, userId);
        await Clients
            .OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("Typing", new { conversationId, userId });
    }

    public async Task StopTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await EnsureParticipant(conversationId, userId);
        await Clients
            .OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("StopTyping", new { conversationId, userId });
    }

    public async Task MarkAsRead(Guid conversationId, Guid messageId)
    {
        var userId = GetCurrentUserId();
        await _mediator.Send(new MarkConversationAsReadCommand(conversationId, messageId, userId));
    }

    private async Task EnsureParticipant(Guid conversationId, Guid userId)
    {
        var canAccess = await _mediator.Send(new EnsureConversationParticipantQuery(conversationId, userId));

        if (!canAccess)
        {
            throw new HubException("You are not a participant in this conversation.");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Context.User?.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new HubException("Invalid token.");
        }

        return userId;
    }
}
