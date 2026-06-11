using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Chat.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Messages.Send.Commands;

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

        var existingMessage = await _context.Set<Message>()
            .FirstOrDefaultAsync(
                message =>
                    message.ConversationsId == request.ConversationId &&
                    message.SenderUserId == command.UserId &&
                    message.ClientMessageId == request.ClientMessageId,
                cancellationToken);

        if (existingMessage is not null)
        {
            return ToResponse(existingMessage);
        }

        var now = _dateTimeService.UtcNow;
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
            ClientMessageId = request.ClientMessageId.Trim(),
            SentAt = now
        };

        _context.Set<Message>().Add(message);

        foreach (var attachment in attachments)
        {
            _context.Set<MessageAttachment>().Add(new MessageAttachment
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
            });
        }

        conversation.LastMessageId = message.MessagesId;
        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;
        IncrementUnreadCounts(request.ConversationId, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var response = ToResponse(message);
        await _chatRealtimeNotifier.SendConversationEventAsync(
            request.ConversationId,
            "ReceiveMessage",
            response,
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

    private void IncrementUnreadCounts(Guid conversationId, Guid senderUserId)
    {
        var participants = _context.Set<ConversationParticipant>()
            .Where(participant =>
                participant.ConversationsId == conversationId &&
                participant.LeftAt == null);

        foreach (var participant in participants)
        {
            if (participant.UserId != senderUserId)
            {
                participant.UnreadCount += 1;
            }
        }
    }

    private static MessageResponse ToResponse(Message message)
    {
        var isDeleted = message.DeletedForEveryoneAt.HasValue;

        return new MessageResponse(
            message.MessagesId,
            message.ConversationsId,
            message.SenderUserId,
            message.MessageType,
            isDeleted ? null : message.Content,
            isDeleted ? null : message.Metadata,
            message.SentAt,
            isDeleted);
    }
}
