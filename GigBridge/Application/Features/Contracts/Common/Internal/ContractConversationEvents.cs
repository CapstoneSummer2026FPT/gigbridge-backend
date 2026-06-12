using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractConversationEvents
{
    public static async Task AddSystemMessageAsync(
        IApplicationDbContext context,
        Guid contractId,
        string content,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var conversation = await context.Set<Conversation>()
            .Where(conversation => conversation.ContractsId == contractId)
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return;
        }

        AddSystemMessage(context, conversation, content, now);
    }

    public static void AddSystemMessage(
        IApplicationDbContext context,
        Conversation conversation,
        string content,
        DateTime now)
    {
        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            SenderUserId = null,
            MessageType = (int)MessageType.ContractEvent,
            Content = content,
            SentAt = now
        };

        context.Set<Message>().Add(message);
        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
    }
}
