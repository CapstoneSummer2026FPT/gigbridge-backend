using Application.Common.Interfaces;
using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.JobPosts.Common;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Disputes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Conversations.GetMine.Queries;

public class GetMyConversationsQueryHandler
    : IRequestHandler<GetMyConversationsQuery, IReadOnlyList<ConversationSummaryResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetMyConversationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConversationSummaryResponse>> Handle(
        GetMyConversationsQuery request,
        CancellationToken cancellationToken)
    {
        var participants = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(participant =>
                participant.UserId == request.UserId &&
                participant.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var conversationIds = participants
            .Select(participant => participant.ConversationsId)
            .ToHashSet();

        var conversations = await _context.Set<Conversation>()
            .AsNoTracking()
            .Where(conversation =>
                conversationIds.Contains(conversation.ConversationsId) &&
                conversation.DeletedAt == null)
            .Include(conversation => conversation.JobPosts)
                .ThenInclude(jobPost => jobPost!.MajorCategory)
                    .ThenInclude(majorCategory => majorCategory!.Category)
            .Include(conversation => conversation.Proposals)
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .ToListAsync(cancellationToken);

        var lastMessageIds = conversations
            .Where(conversation => conversation.LastMessageId.HasValue)
            .Select(conversation => conversation.LastMessageId!.Value)
            .ToHashSet();

        var lastMessages = await _context.Set<Message>()
            .AsNoTracking()
            .Where(message => lastMessageIds.Contains(message.MessagesId))
            .ToDictionaryAsync(message => message.MessagesId, cancellationToken);

        var lastMessageAttachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment => lastMessageIds.Contains(attachment.MessagesId))
            .ToListAsync(cancellationToken);

        var attachmentsByMessage = lastMessageAttachments
            .GroupBy(attachment => attachment.MessagesId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ToAttachmentResponse).ToList());

        var unreadByConversation = participants.ToDictionary(
            participant => participant.ConversationsId,
            participant => participant.UnreadCount);
        var roleByConversation = participants.ToDictionary(
            participant => participant.ConversationsId,
            participant => participant.ParticipantRole);

        var allParticipants = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationsId) && p.DeletedAt == null)
            .Include(p => p.User)
                .ThenInclude(u => u.FreelancerProfile)
            .Include(p => p.User)
                .ThenInclude(u => u.ClientProfile)
            .ToListAsync(cancellationToken);

        var otherParticipantsLookup = allParticipants
            .Where(p => p.UserId != request.UserId)
            .GroupBy(p => p.ConversationsId)
            .ToDictionary(
                g => g.Key,
                g => g.First()
            );

        var latestOffers = await _context.Set<NegotiationOffer>()
            .AsNoTracking()
            .Where(offer => conversationIds.Contains(offer.ConversationsId))
            .OrderByDescending(offer => offer.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestOffersLookup = latestOffers
            .GroupBy(offer => offer.ConversationsId)
            .ToDictionary(
                g => g.Key,
                g => g.First()
            );

        return conversations
            .Select(conversation => {
                otherParticipantsLookup.TryGetValue(conversation.ConversationsId, out var otherParticipant);
                latestOffersLookup.TryGetValue(conversation.ConversationsId, out var latestOffer);
                Message? message = null;
                var hasLastMessage = conversation.LastMessageId.HasValue &&
                    lastMessages.TryGetValue(conversation.LastMessageId.Value, out message);
                var isVisibleLastMessage = !hasLastMessage ||
                    conversation.ConversationType != (int)ConversationType.Dispute ||
                    IsVisibleToParticipant(message!, request.UserId,
                        roleByConversation.GetValueOrDefault(conversation.ConversationsId));

                return new ConversationSummaryResponse(
                    conversation.ConversationsId,
                    conversation.ConversationType,
                    conversation.Title ?? conversation.JobPosts?.Title,
                    conversation.JobPostsId,
                    conversation.ProposalsId,
                    conversation.ContractsId,
                    conversation.DisputesId,
                    conversation.Status,
                    unreadByConversation.GetValueOrDefault(conversation.ConversationsId),
                    conversation.CreatedAt,
                    isVisibleLastMessage ? conversation.LastMessageAt : null,
                    hasLastMessage && isVisibleLastMessage
                        ? ToMessageResponse(
                            message!,
                            attachmentsByMessage.GetValueOrDefault(message!.MessagesId) ?? [])
                        : null,
                    otherParticipant?.UserId,
                    otherParticipant?.User?.FullName,
                    otherParticipant?.User?.Avatar,
                    otherParticipant?.User?.Role,
                    otherParticipant?.User?.ClientProfile?.CompanyName,
                    otherParticipant?.User?.FreelancerProfile?.Title,
                    latestOffer?.NegotiationOfferId,
                    latestOffer?.FinalPrice,
                    latestOffer?.Status,
                    conversation.JobPosts?.BudgetMin,
                    conversation.JobPosts?.BudgetMax,
                    conversation.JobPosts?.Currency,
                    conversation.JobPosts?.MajorCategory?.Category?.Name,
                    conversation.Proposals?.ProposedBudget,
                    conversation.Proposals?.ProposedDuration,
                    conversation.JobPosts?.Status,
                    conversation.JobPosts?.Visibility,
                    conversation.JobPosts is not null &&
                        JobPostNegotiationGuard.IsEligibleForNegotiation(
                            conversation.JobPosts.Status,
                            conversation.JobPosts.Visibility)
                );
            })
            .ToList();
    }

    private static bool IsVisibleToParticipant(Message message, Guid userId, int participantRole)
    {
        if (participantRole == (int)ParticipantRole.Admin ||
            message.SenderUserId == userId ||
            message.DisputeRecipient is null ||
            message.DisputeRecipient == DisputeMessageRecipient.Both)
            return true;

        return (message.DisputeRecipient == DisputeMessageRecipient.Client &&
                participantRole == (int)ParticipantRole.Client) ||
               (message.DisputeRecipient == DisputeMessageRecipient.Freelancer &&
                participantRole == (int)ParticipantRole.Freelancer);
    }

    private static MessageResponse ToMessageResponse(
        Message message,
        IReadOnlyList<MessageAttachmentResponse> attachments)
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
