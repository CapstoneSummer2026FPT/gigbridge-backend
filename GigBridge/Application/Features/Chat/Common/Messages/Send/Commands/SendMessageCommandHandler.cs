using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Messages.Send.Commands;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageResponse>
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".bat",
        ".cmd",
        ".sh"
    };

    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<MessageResponse> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        ValidateRequest(request);
        var attachments = request.Attachments ?? Array.Empty<SendMessageAttachmentRequest>();
        var clientMessageId = request.ClientMessageId.Trim();

        var conversation = await _context.Set<Conversation>()
            .FirstOrDefaultAsync(
                conversation => conversation.ConversationsId == request.ConversationId,
                cancellationToken);

        if (conversation is null)
        {
            throw new NotFoundException("Conversation does not exist.");
        }

        if (conversation.Status != (int)ConversationStatus.Active)
        {
            throw new BadRequestException("Conversation is not active.");
        }

        var participant = await _context.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == command.UserId &&
                    participant.LeftAt == null,
                cancellationToken);

        if (participant is null)
        {
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
        }

        await EnsureReplyTargetBelongsToConversation(request, cancellationToken);

        var existingMessage = await _context.Set<Message>()
            .FirstOrDefaultAsync(
                message =>
                    message.ConversationsId == request.ConversationId &&
                    message.SenderUserId == command.UserId &&
                    message.ClientMessageId == clientMessageId,
                cancellationToken);

        if (existingMessage is not null)
        {
            var existingAttachments = await GetMessageAttachments(
                existingMessage.MessagesId,
                cancellationToken);

            return ToResponse(existingMessage, existingAttachments);
        }

        var now = _dateTimeService.UtcNow;
        var activeParticipants = await _context.Set<ConversationParticipant>()
            .Where(participant =>
                participant.ConversationsId == request.ConversationId &&
                participant.LeftAt == null &&
                participant.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = request.ConversationId,
            SenderUserId = command.UserId,
            MessageType = attachments.Count > 0
                ? (int)MessageType.File
                : (int)MessageType.Text,
            Content = string.IsNullOrWhiteSpace(request.Content)
                ? null
                : request.Content.Trim(),
            ReplyToMessageId = request.ReplyToMessageId,
            ClientMessageId = clientMessageId,
            SentAt = now
        };

        _context.Set<Message>().Add(message);

        var messageAttachments = new List<MessageAttachment>();

        foreach (var attachment in attachments)
        {
            var messageAttachment = new MessageAttachment
            {
                MessageAttachmentsId = Guid.NewGuid(),
                MessagesId = message.MessagesId,
                FileName = attachment.FileName.Trim(),
                FileUrl = attachment.FileUrl.Trim(),
                StorageProvider = attachment.StorageProvider.Trim(),
                StorageObjectKey = attachment.StorageObjectKey,
                MimeType = attachment.MimeType.Trim(),
                FileExtension = attachment.FileExtension,
                FileSizeBytes = attachment.FileSizeBytes,
                CreatedAt = now
            };

            _context.Set<MessageAttachment>().Add(messageAttachment);
            messageAttachments.Add(messageAttachment);
        }

        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
        IncrementUnreadCounts(activeParticipants, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var response = ToResponse(message, messageAttachments);
        var participantUserIds = activeParticipants
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArray();

        await _chatRealtimeNotifier.SendUsersEventAsync(
            participantUserIds,
            "ReceiveMessage",
            response,
            cancellationToken);

        await SendConversationUpdatedEvents(
            activeParticipants,
            response,
            response.SentAt,
            cancellationToken);

        return response;
    }

    private static void ValidateRequest(SendMessageRequest request)
    {
        if (request.ConversationId == Guid.Empty)
        {
            throw new BadRequestException("ConversationId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            throw new BadRequestException("clientMessageId is required.");
        }

        var attachments = request.Attachments ?? [];

        if (string.IsNullOrWhiteSpace(request.Content) && attachments.Count == 0)
        {
            throw new BadRequestException("Message content or attachment is required.");
        }

        foreach (var attachment in attachments)
        {
            if (attachment.FileSizeBytes <= 0 || attachment.FileSizeBytes > MaxFileSizeBytes)
            {
                throw new BadRequestException("Attachment file size is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(attachment.FileExtension) &&
                BlockedExtensions.Contains(attachment.FileExtension))
            {
                throw new BadRequestException("Attachment file extension is not allowed.");
            }
        }
    }

    private async Task EnsureReplyTargetBelongsToConversation(
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ReplyToMessageId.HasValue)
        {
            return;
        }

        var replyExists = await _context.Set<Message>()
            .AsNoTracking()
            .AnyAsync(
                message =>
                    message.MessagesId == request.ReplyToMessageId.Value &&
                    message.ConversationsId == request.ConversationId,
                cancellationToken);

        if (!replyExists)
        {
            throw new BadRequestException("ReplyToMessageId must belong to the same conversation.");
        }
    }

    private Task<List<MessageAttachment>> GetMessageAttachments(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment => attachment.MessagesId == messageId)
            .ToListAsync(cancellationToken);
    }

    private static void IncrementUnreadCounts(
        IReadOnlyCollection<ConversationParticipant> participants,
        Guid senderUserId)
    {
        foreach (var participant in participants)
        {
            if (participant.UserId != senderUserId)
            {
                participant.UnreadCount += 1;
            }
        }
    }

    private async Task SendConversationUpdatedEvents(
        IReadOnlyCollection<ConversationParticipant> participants,
        MessageResponse lastMessage,
        DateTime lastMessageAt,
        CancellationToken cancellationToken)
    {
        foreach (var participant in participants
            .GroupBy(participant => participant.UserId)
            .Select(group => group.First()))
        {
            await _chatRealtimeNotifier.SendUserEventAsync(
                participant.UserId,
                "ConversationUpdated",
                new
                {
                    conversationId = lastMessage.ConversationId,
                    lastMessage,
                    lastMessageAt,
                    unreadCount = participant.UnreadCount
                },
                cancellationToken);
        }
    }

    private static MessageResponse ToResponse(
        Message message,
        IReadOnlyList<MessageAttachment> attachments)
    {
        var isDeleted = message.DeletedForEveryoneAt.HasValue;

        return new MessageResponse(
            message.MessagesId,
            message.ConversationsId,
            message.SenderUserId,
            message.MessageType,
            isDeleted ? null : message.Content,
            message.ReplyToMessageId,
            isDeleted ? null : message.Metadata,
            message.ClientMessageId,
            message.SentAt,
            isDeleted ? null : message.EditedAt,
            isDeleted,
            isDeleted
                ? []
                : attachments.Select(ToAttachmentResponse).ToList());
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
