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
            var senderUser = await _context.Set<User>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == command.UserId && u.IsActive, cancellationToken);

            if (senderUser?.Role == (int)UserRole.Admin &&
                (conversation.ConversationType == (int)ConversationType.Dispute || conversation.DisputesId.HasValue))
            {
                participant = new ConversationParticipant
                {
                    ConversationParticipantId = Guid.NewGuid(),
                    ConversationsId = request.ConversationId,
                    UserId = command.UserId,
                    JoinedAt = _dateTimeService.UtcNow,
                    ParticipantRole = (int)ParticipantRole.Admin
                };
                _context.Set<ConversationParticipant>().Add(participant);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new ForbiddenAccessException("You are not a participant in this conversation.");
            }
        }

        await EnsureConversationWritable(conversation, participant, cancellationToken);

        await EnsureReplyTargetBelongsToConversation(request, cancellationToken);

        var existingMessage = await _context.Set<Message>()
            .FirstOrDefaultAsync(
                message =>
                    message.SenderUserId == command.UserId &&
                    message.ClientMessageId == clientMessageId,
                cancellationToken);

        if (existingMessage is not null)
        {
            var existingAttachments = await GetMessageAttachments(
                existingMessage.MessagesId,
                cancellationToken);
            var existingSender = await GetSenderAsync(existingMessage.SenderUserId, cancellationToken);
            return ToResponse(existingMessage, existingAttachments, existingSender);
        }

        var now = _dateTimeService.UtcNow;
        var activeParticipants = await _context.Set<ConversationParticipant>()
            .Where(participant =>
                participant.ConversationsId == request.ConversationId &&
                participant.LeftAt == null &&
                participant.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var recipient = ResolveDisputeRecipient(conversation, participant, command.DisputeRecipient);
        var deliveryParticipants = ResolveDeliveryParticipants(
            conversation,
            activeParticipants,
            recipient);

        var message = new Message
        {
            MessagesId = Guid.NewGuid(),
            ConversationsId = request.ConversationId,
            SenderUserId = command.UserId,
            MessageType = participant.ParticipantRole == (int)ParticipantRole.Admin &&
                          conversation.ConversationType == (int)ConversationType.Dispute
                ? (int)MessageType.AdminOfficial
                : attachments.Count > 0
                    ? (int)MessageType.File
                    : (int)MessageType.Text,
            Content = string.IsNullOrWhiteSpace(request.Content)
                ? null
                : request.Content.Trim(),
            ReplyToMessageId = request.ReplyToMessageId,
            Metadata = command.ServerMetadata,
            ClientMessageId = clientMessageId,
            DisputeRecipient = recipient,
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
        IncrementUnreadCounts(deliveryParticipants, command.UserId);

        await _context.SaveChangesAsync(cancellationToken);

        var sender = await GetSenderAsync(command.UserId, cancellationToken);
        var response = ToResponse(message, messageAttachments, sender);
        var participantUserIds = deliveryParticipants
            .Select(participant => participant.UserId)
            .Distinct()
            .ToArray();

        await _chatRealtimeNotifier.SendUsersEventAsync(
            participantUserIds,
            "ReceiveMessage",
            response,
            cancellationToken);

        await SendConversationUpdatedEvents(
            deliveryParticipants,
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

        var reply = await _context.Set<Message>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                message =>
                    message.MessagesId == request.ReplyToMessageId.Value &&
                    message.ConversationsId == request.ConversationId,
                cancellationToken);

        if (reply is null)
        {
            throw new BadRequestException("ReplyToMessageId must belong to the same conversation.");
        }
    }

    private static DisputeMessageRecipient? ResolveDisputeRecipient(
        Conversation conversation,
        ConversationParticipant sender,
        DisputeMessageRecipient? requestedRecipient)
    {
        if (conversation.ConversationType != (int)ConversationType.Dispute)
        {
            if (requestedRecipient.HasValue)
                throw new BadRequestException("Recipient targeting is only available for dispute conversations.");
            return null;
        }

        if (sender.ParticipantRole == (int)ParticipantRole.Admin)
        {
            return requestedRecipient
                ?? throw new BadRequestException("Administrators must choose Client, Freelancer, or Both.");
        }

        if (requestedRecipient.HasValue)
            throw new ForbiddenAccessException("Only an administrator may choose dispute message recipients.");

        return sender.ParticipantRole switch
        {
            (int)ParticipantRole.Client => DisputeMessageRecipient.Client,
            (int)ParticipantRole.Freelancer => DisputeMessageRecipient.Freelancer,
            _ => throw new ForbiddenAccessException("Only the Client, Freelancer, or assigned administrator may send dispute messages.")
        };
    }

    private static IReadOnlyCollection<ConversationParticipant> ResolveDeliveryParticipants(
        Conversation conversation,
        IReadOnlyCollection<ConversationParticipant> activeParticipants,
        DisputeMessageRecipient? recipient)
    {
        if (conversation.ConversationType != (int)ConversationType.Dispute || !recipient.HasValue)
            return activeParticipants;

        return activeParticipants.Where(participant =>
            participant.ParticipantRole == (int)ParticipantRole.Admin ||
            recipient == DisputeMessageRecipient.Both ||
            (recipient == DisputeMessageRecipient.Client &&
             participant.ParticipantRole == (int)ParticipantRole.Client) ||
            (recipient == DisputeMessageRecipient.Freelancer &&
             participant.ParticipantRole == (int)ParticipantRole.Freelancer))
            .ToList();
    }

    private async Task EnsureConversationWritable(
        Conversation conversation,
        ConversationParticipant participant,
        CancellationToken cancellationToken)
    {
        if (conversation.ConversationType == (int)ConversationType.ContractWorkroom &&
            participant.ParticipantRole == (int)ParticipantRole.Admin)
        {
            throw new ForbiddenAccessException("Administrators may only read Workspace conversations.");
        }

        if (conversation.ConversationType != (int)ConversationType.ContractWorkroom ||
            !conversation.ContractsId.HasValue)
        {
            return;
        }

        var contractStatus = await _context.Set<Contract>()
            .AsNoTracking()
            .Where(contract => contract.ContractsId == conversation.ContractsId.Value)
            .Select(contract => (int?)contract.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (contractStatus == (int)ContractStatus.Disputed)
        {
            throw new BadRequestException(
                "This contract is currently under dispute. Please continue communication in the dispute conversation.");
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

    private async Task<User?> GetSenderAsync(Guid? senderUserId, CancellationToken cancellationToken)
    {
        if (!senderUserId.HasValue) return null;
        return await _context.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == senderUserId.Value, cancellationToken);
    }

    private static MessageResponse ToResponse(
        Message message,
        IReadOnlyList<MessageAttachment> attachments,
        User? sender = null)
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
                : attachments.Select(ToAttachmentResponse).ToList(),
            SenderName: sender?.FullName,
            SenderAvatar: sender?.Avatar,
            SenderRole: sender?.Role,
            DisputeRecipient: message.DisputeRecipient);
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
