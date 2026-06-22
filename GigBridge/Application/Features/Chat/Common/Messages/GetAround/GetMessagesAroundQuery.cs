using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Chat.Common.Messages;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chat.Common.Messages.GetAround;

public record GetMessagesAroundQuery(Guid ConversationId, Guid MessageId, Guid UserId, int Radius = 20)
    : IRequest<IReadOnlyList<ConversationMessageResponse>>;

public class GetMessagesAroundQueryHandler : IRequestHandler<GetMessagesAroundQuery, IReadOnlyList<ConversationMessageResponse>>
{
    private readonly IApplicationDbContext _context;
    public GetMessagesAroundQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<ConversationMessageResponse>> Handle(GetMessagesAroundQuery request, CancellationToken ct)
    {
        var participant = await _context.Set<ConversationParticipant>().AsNoTracking().AnyAsync(x =>
            x.ConversationsId == request.ConversationId && x.UserId == request.UserId && x.LeftAt == null && x.DeletedAt == null, ct);
        if (!participant) throw new ForbiddenAccessException("You are not a participant in this conversation.");
        var anchor = await _context.Set<Message>().AsNoTracking().FirstOrDefaultAsync(x =>
            x.MessagesId == request.MessageId && x.ConversationsId == request.ConversationId, ct)
            ?? throw new NotFoundException("Anchor message not found.");
        var radius = Math.Clamp(request.Radius, 1, 50);
        var before = await _context.Set<Message>().AsNoTracking().Where(x => x.ConversationsId == request.ConversationId &&
            x.DeletedForSenderAt == null && (x.SentAt < anchor.SentAt || x.SentAt == anchor.SentAt && x.MessagesId.CompareTo(anchor.MessagesId) < 0))
            .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.MessagesId).Take(radius).ToListAsync(ct);
        var after = await _context.Set<Message>().AsNoTracking().Where(x => x.ConversationsId == request.ConversationId &&
            x.DeletedForSenderAt == null && (x.SentAt > anchor.SentAt || x.SentAt == anchor.SentAt && x.MessagesId.CompareTo(anchor.MessagesId) > 0))
            .OrderBy(x => x.SentAt).ThenBy(x => x.MessagesId).Take(radius).ToListAsync(ct);
        var messages = before.Append(anchor).Concat(after).OrderBy(x => x.SentAt).ThenBy(x => x.MessagesId).ToList();
        var ids = messages.Select(x => x.MessagesId).ToHashSet();
        var attachments = await _context.Set<MessageAttachment>().AsNoTracking().Where(x => ids.Contains(x.MessagesId)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return messages.Select(m =>
        {
            var isDeleted = m.DeletedForEveryoneAt.HasValue;
            IReadOnlyList<MessageAttachmentResponse> messageAttachments = isDeleted
                ? []
                : attachments.Where(a => a.MessagesId == m.MessagesId).Select(a => new MessageAttachmentResponse(
                    a.MessageAttachmentsId, a.FileName, a.FileUrl, a.StorageProvider, a.StorageObjectKey, a.MimeType,
                    a.FileExtension, a.FileSizeBytes, a.CreatedAt)).ToList();

            return new ConversationMessageResponse(m.MessagesId, m.ConversationsId, m.SenderUserId,
                m.MessageType, isDeleted ? null : m.Content, m.ReplyToMessageId,
                isDeleted ? null : m.Metadata, m.ClientMessageId, m.SentAt, isDeleted ? null : m.EditedAt,
                isDeleted, messageAttachments,
                isDeleted ? null : MessageHelpers.ParseScheduleMetadata(m, request.UserId, now));
        }).ToList();
    }
}
