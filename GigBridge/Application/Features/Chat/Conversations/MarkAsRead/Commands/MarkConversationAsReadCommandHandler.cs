using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Conversations.MarkAsRead.Commands;

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

        var messageExists = await _context.Set<Message>()
            .AnyAsync(
                message =>
                    message.ConversationsId == request.ConversationId &&
                    message.MessagesId == request.MessageId,
                cancellationToken);

        if (!messageExists)
        {
            throw new NotFoundException("Message does not exist in this conversation.");
        }

        participant.LastReadMessageId = request.MessageId;
        participant.LastReadAt = _dateTimeService.UtcNow;
        participant.UnreadCount = 0;

        await _context.SaveChangesAsync(cancellationToken);

        await _chatRealtimeNotifier.SendConversationEventAsync(
            request.ConversationId,
            "ConversationRead",
            new { userId = request.UserId, messageId = request.MessageId },
            cancellationToken);

        return true;
    }
}
