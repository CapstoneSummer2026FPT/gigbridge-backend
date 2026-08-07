using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Messages;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;

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
        var participant = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                participant =>
                    participant.ConversationsId == request.ConversationId &&
                    participant.UserId == request.UserId &&
                    participant.LeftAt == null &&
                    participant.DeletedAt == null,
                cancellationToken);

        var canAdminRead = participant is null && await CanAdminReadDisputeConversationAsync(request, cancellationToken);
        if (participant is null && !canAdminRead)
        {
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
        }

        var conversationType = await _context.Set<Conversation>().AsNoTracking()
            .Where(conversation => conversation.ConversationsId == request.ConversationId)
            .Select(conversation => (int?)conversation.ConversationType)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Conversation does not exist.");

        var pageSize = request.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(request.PageSize, MaxPageSize);

        var query = _context.Set<Message>()
            .AsNoTracking()
            .Where(message =>
                message.ConversationsId == request.ConversationId &&
                message.DeletedForSenderAt == null);

        if (conversationType == (int)ConversationType.Dispute)
        {
            var isUserAdmin = participant?.ParticipantRole == (int)ParticipantRole.Admin || canAdminRead;
            if (!isUserAdmin)
            {
                // Non-admin participant: only see own messages + messages addressed to their party
                var recipient = participant?.ParticipantRole == (int)ParticipantRole.Client
                    ? DisputeMessageRecipient.Client
                    : DisputeMessageRecipient.Freelancer;
                query = query.Where(message =>
                    message.SenderUserId == request.UserId ||
                    message.DisputeRecipient == null ||
                    message.DisputeRecipient == DisputeMessageRecipient.Both ||
                    message.DisputeRecipient == recipient);
            }
        }

        if (request.Before.HasValue)
        {
            query = query.Where(message => message.SentAt < request.Before.Value);
        }

        var messages = await query
            .OrderByDescending(message => message.SentAt)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        messages = messages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.MessagesId)
            .ToList();

        var messageIds = messages.Select(message => message.MessagesId).ToHashSet();
        var attachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment => messageIds.Contains(attachment.MessagesId))
            .ToListAsync(cancellationToken);

        var attachmentsByMessage = attachments
            .GroupBy(attachment => attachment.MessagesId)
            .ToDictionary(group => group.Key, group => group.Select(ToAttachmentResponse).ToList());
        var senderIds = messages.Where(message => message.SenderUserId.HasValue)
            .Select(message => message.SenderUserId!.Value).ToHashSet();
        var senders = await _context.Set<User>().AsNoTracking()
            .Where(user => senderIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);
        var scheduleIds = messages
            .Where(message => message.ScheduleId.HasValue)
            .Select(message => message.ScheduleId!.Value)
            .ToHashSet();
        var schedulesById = await _context.Set<Schedule>()
            .AsNoTracking()
            .Where(schedule => scheduleIds.Contains(schedule.ScheduleId))
            .ToDictionaryAsync(schedule => schedule.ScheduleId, cancellationToken);
        var now = DateTime.UtcNow;

        return messages
            .Select(message => ToMessageResponse(
                message,
                attachmentsByMessage.GetValueOrDefault(message.MessagesId) ?? [],
                request.UserId,
                now,
                message.ScheduleId.HasValue
                    ? schedulesById.GetValueOrDefault(message.ScheduleId.Value)
                    : null,
                message.SenderUserId.HasValue
                    ? senders.GetValueOrDefault(message.SenderUserId.Value)
                    : null))
            .ToList();
    }

    private async Task<bool> CanAdminReadDisputeConversationAsync(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.AdminDisputeId.HasValue)
            return false;

        var isAdmin = await _context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user => user.UserId == request.UserId &&
                              user.Role == (int)Domain.Enums.UserRole.Admin &&
                              user.IsActive,
                cancellationToken);
        if (!isAdmin)
            return false;

        var disputeContractId = await _context.Set<Dispute>()
            .AsNoTracking()
            .Where(dispute => dispute.DisputesId == request.AdminDisputeId.Value)
            .Select(dispute => (Guid?)dispute.ContractsId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!disputeContractId.HasValue)
            return false;

        return await _context.Set<Conversation>()
            .AsNoTracking()
            .AnyAsync(conversation => conversation.ConversationsId == request.ConversationId &&
                                      (conversation.DisputesId == request.AdminDisputeId ||
                                       (conversation.ContractsId == disputeContractId &&
                                        conversation.ConversationType == (int)Domain.Enums.ConversationType.ContractWorkroom)),
                cancellationToken);
    }

    private static ConversationMessageResponse ToMessageResponse(
        Message message,
        IReadOnlyList<MessageAttachmentResponse> attachments,
        Guid viewerUserId,
        DateTime utcNow,
        Schedule? currentSchedule,
        User? sender)
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
            message.ClientMessageId,
            message.SentAt,
            isDeleted ? null : message.EditedAt,
            isDeleted,
            isDeleted ? [] : attachments,
            isDeleted ? null : MessageHelpers.ParseScheduleMetadata(message, viewerUserId, utcNow, currentSchedule),
            sender?.FullName,
            sender?.Avatar,
            sender?.Role,
            message.DisputeRecipient);
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
