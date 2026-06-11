using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Messages.GetConversationMessages.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Messages.GetConversationMessages.Queries;

public class GetConversationMessagesQueryHandler
    : IRequestHandler<GetConversationMessagesQuery, IReadOnlyList<ConversationMessageResponse>>
{
    private const int DefaultPageSize = 30;
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _context;

    public GetConversationMessagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> Handle(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var isParticipant = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .AnyAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == request.UserId &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null,
                cancellationToken);

        if (!isParticipant)
        {
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
        }

        var pageSize = request.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(request.PageSize, MaxPageSize);

        var query = _context.Set<Message>()
            .AsNoTracking()
            .Where(message =>
                message.ConversationsId == request.ConversationId &&
                message.DeletedForSenderAt == null);

        if (request.Before.HasValue)
        {
            query = query.Where(message => message.SentAt < request.Before.Value);
        }

        var messages = await query
            .OrderByDescending(message => message.SentAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var messageIds = messages.Select(message => message.MessagesId).ToHashSet();
        var attachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment => messageIds.Contains(attachment.MessagesId))
            .ToListAsync(cancellationToken);

        var attachmentsByMessage = attachments
            .GroupBy(attachment => attachment.MessagesId)
            .ToDictionary(group => group.Key, group => group.Select(ToAttachmentResponse).ToList());

        return messages
            .Select(message => ToMessageResponse(
                message,
                attachmentsByMessage.GetValueOrDefault(message.MessagesId) ?? []))
            .ToList();
    }

    private static ConversationMessageResponse ToMessageResponse(
        Message message,
        IReadOnlyList<MessageAttachmentResponse> attachments)
    {
        var isDeleted = message.DeletedForEveryoneAt.HasValue;

        return new ConversationMessageResponse(
            message.MessagesId,
            message.ConversationsId,
            message.SenderUserId,
            message.MessageType,
            isDeleted ? null : message.Content,
            message.ReplyToMessageId,
            isDeleted ? null : message.Metadata,
            message.SentAt,
            isDeleted ? null : message.EditedAt,
            isDeleted,
            isDeleted ? [] : attachments);
    }

    private static MessageAttachmentResponse ToAttachmentResponse(MessageAttachment attachment)
    {
        return new MessageAttachmentResponse(
            attachment.MessageAttachmentsId,
            attachment.FileName,
            attachment.FileUrl,
            attachment.StorageProvider,
            attachment.StorageObjectKey,
            attachment.MimeType,
            attachment.FileExtension,
            attachment.FileSizeBytes,
            attachment.CreatedAt);
    }
}
