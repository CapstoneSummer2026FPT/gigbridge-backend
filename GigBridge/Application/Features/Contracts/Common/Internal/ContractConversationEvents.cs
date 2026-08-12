using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums.Chat;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractConversationEvents
{
    public static async Task<Message?> AddSystemMessageAsync(
        IApplicationDbContext context,
        Guid contractId,
        string content,
        DateTime now,
        CancellationToken cancellationToken,
        string? metadata = null)
    {
        var conversation = await context.Set<Conversation>()
            .Where(conversation => conversation.ContractsId == contractId)
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return null;
        }

        return AddSystemMessage(context, conversation, content, now, metadata);
    }

    public static Message AddSystemMessage(
        IApplicationDbContext context,
        Conversation conversation,
        string content,
        DateTime now,
        string? metadata = null)
    {
        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = conversation.ConversationsId,
            SenderUserId = null,
            MessageType = (int)MessageType.ContractEvent,
            Content = content,
            Metadata = metadata,
            SentAt = now
        };

        context.Set<Message>().Add(message);
        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;

        return message;
    }

    /// <summary>
    /// Locks the workspace and dispute conversations of a contract so neither party can
    /// send messages anymore. Used when a contract is completed or terminated.
    /// </summary>
    public static async Task CloseContractConversationsAsync(
        IApplicationDbContext context,
        Guid contractId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var conversations = await context.Set<Conversation>()
            .Where(conversation =>
                conversation.ContractsId == contractId &&
                conversation.DeletedAt == null &&
                (conversation.ConversationType == (int)ConversationType.ContractWorkroom ||
                 conversation.ConversationType == (int)ConversationType.Dispute))
            .ToListAsync(cancellationToken);

        foreach (var conversation in conversations)
        {
            conversation.Status = (int)ConversationStatus.Closed;
            conversation.UpdatedAt = now;
        }
    }
}
