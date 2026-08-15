using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Chat.Interfaces;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Disputes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;

public class MarkConversationAsReadCommandHandler
    : IRequestHandler<MarkConversationAsReadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public MarkConversationAsReadCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<bool> Handle(
        MarkConversationAsReadCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await _context.Set<ConversationParticipant>()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == request.UserId &&
                    participant.LeftAt == null,
                cancellationToken);

        if (participant is null)
        {
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
        }

        var message = await _context.Set<Message>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                message =>
                    message.ConversationsId == request.ConversationId &&
                    message.MessagesId == request.MessageId,
                cancellationToken);

        if (message is null)
        {
            throw new NotFoundException("Message does not exist in this conversation.");
        }

        var conversationType = await _context.Set<Conversation>().AsNoTracking()
            .Where(conversation => conversation.ConversationsId == request.ConversationId)
            .Select(conversation => (int?)conversation.ConversationType)
            .FirstOrDefaultAsync(cancellationToken);
        if (conversationType == (int)ConversationType.Dispute &&
            !IsVisibleToParticipant(message, request.UserId, participant.ParticipantRole))
            throw new ForbiddenAccessException("This dispute message is not visible to you.");

        participant.LastReadMessageId = request.MessageId;
        participant.LastReadAt = _dateTimeService.UtcNow;
        participant.UnreadCount = 0;

        await _context.SaveChangesAsync(cancellationToken);

        var conversation = await _context.Set<Conversation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                conversation => conversation.ConversationsId == request.ConversationId,
                cancellationToken);

        var lastMessage = await GetLastMessageResponse(conversation, participant, cancellationToken);

        await _chatRealtimeNotifier.SendUserEventAsync(
            request.UserId,
            "ConversationUpdated",
            new
            {
                conversationId = request.ConversationId,
                lastMessage,
                lastMessageAt = conversation?.LastMessageAt,
                unreadCount = 0
            },
            cancellationToken);

        return true;
    }

    private static bool IsVisibleToParticipant(Message message, Guid userId, int participantRole) =>
        participantRole == (int)ParticipantRole.Admin || message.SenderUserId == userId ||
        message.DisputeRecipient is null || message.DisputeRecipient == DisputeMessageRecipient.Both ||
        (message.DisputeRecipient == DisputeMessageRecipient.Client && participantRole == (int)ParticipantRole.Client) ||
        (message.DisputeRecipient == DisputeMessageRecipient.Freelancer && participantRole == (int)ParticipantRole.Freelancer);

    private async Task<MessageResponse?> GetLastMessageResponse(
        Conversation? conversation,
        ConversationParticipant participant,
        CancellationToken cancellationToken)
    {
        if (conversation?.LastMessageId is null)
        {
            return null;
        }

        var message = await _context.Set<Message>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                message => message.MessagesId == conversation.LastMessageId.Value,
                cancellationToken);

        if (message is null)
        {
            return null;
        }
        if (conversation?.ConversationType == (int)ConversationType.Dispute &&
            !IsVisibleToParticipant(message, participant.UserId, participant.ParticipantRole))
            return null;

        var attachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment => attachment.MessagesId == message.MessagesId)
            .ToListAsync(cancellationToken);

        return ToMessageResponse(message, attachments);
    }

    private static MessageResponse ToMessageResponse(
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
