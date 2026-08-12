using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Application.Features.Chat.Common.Conversations.EnsureParticipant.Queries;
using Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;
using MediatR;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Chat;
using Microsoft.EntityFrameworkCore;

namespace Project_API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;

    public ChatHub(IMediator mediator, IApplicationDbContext context)
    {
        _mediator = mediator;
        _context = context;
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
        if (await IsDisputeConversation(conversationId))
            return; // A group typing event could disclose a private dispute thread.
        await Clients
            .OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("Typing", new { conversationId, userId });
    }

    public async Task StopTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();
        await EnsureParticipant(conversationId, userId);
        if (await IsDisputeConversation(conversationId))
            return;
        await Clients
            .OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("StopTyping", new { conversationId, userId });
    }

    private Task<bool> IsDisputeConversation(Guid conversationId) =>
        _context.Set<Conversation>().AsNoTracking().AnyAsync(
            conversation => conversation.ConversationsId == conversationId &&
                            conversation.ConversationType == (int)ConversationType.Dispute);

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
