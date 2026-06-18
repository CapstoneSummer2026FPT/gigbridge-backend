using Application.Common.Interfaces;
using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
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

        var unreadByConversation = participants.ToDictionary(
            participant => participant.ConversationsId,
            participant => participant.UnreadCount);

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
                    conversation.LastMessageAt,
                    conversation.LastMessageId.HasValue &&
                        lastMessages.TryGetValue(conversation.LastMessageId.Value, out var message)
                        ? ToMessageResponse(message)
                        : null,
                    otherParticipant?.UserId,
                    otherParticipant?.User?.FullName,
                    otherParticipant?.User?.Avatar,
                    otherParticipant?.User?.Role,
                    otherParticipant?.User?.ClientProfile?.CompanyName,
                    otherParticipant?.User?.FreelancerProfile?.Title,
                    latestOffer?.NegotiationOfferId,
                    latestOffer?.FinalPrice,
                    latestOffer?.Status
                );
            })
            .ToList();
    }

    private static MessageResponse ToMessageResponse(Message message)
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
