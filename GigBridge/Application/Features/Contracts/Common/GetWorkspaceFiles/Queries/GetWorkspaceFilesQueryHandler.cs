using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetWorkspaceFiles.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Common.GetWorkspaceFiles.Queries;

public class GetWorkspaceFilesQueryHandler
    : IRequestHandler<GetWorkspaceFilesQuery, IReadOnlyList<WorkspaceFileResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetWorkspaceFilesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkspaceFileResponse>> Handle(
        GetWorkspaceFilesQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verify the user is a participant (client or freelancer) of this contract
        var isParticipant = await _context.Set<ConversationParticipant>()
            .AsNoTracking()
            .AnyAsync(participant =>
                participant.UserId == request.UserId &&
                participant.LeftAt == null &&
                participant.DeletedAt == null &&
                participant.Conversations!.ContractsId == request.ContractId &&
                participant.Conversations.ConversationType == (int)ConversationType.ContractWorkroom,
                cancellationToken);

        // Also allow admin access
        var isAdmin = !isParticipant && await _context.Set<User>()
            .AsNoTracking()
            .AnyAsync(user =>
                user.UserId == request.UserId &&
                user.Role == (int)UserRole.Admin &&
                user.IsActive,
                cancellationToken);

        if (!isParticipant && !isAdmin)
        {
            throw new ForbiddenAccessException("You do not have access to this workspace.");
        }

        // 2. Find the ContractWorkroom conversation for this contract
        var workspaceConversationId = await _context.Set<Conversation>()
            .AsNoTracking()
            .Where(conversation =>
                conversation.ContractsId == request.ContractId &&
                conversation.ConversationType == (int)ConversationType.ContractWorkroom &&
                conversation.DeletedAt == null)
            .Select(conversation => (Guid?)conversation.ConversationsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"No workspace conversation found for contract {request.ContractId}.");

        // 3. Fetch all MessageAttachments from messages in this conversation
        //    Only include messages that have not been deleted for everyone
        var attachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment =>
                attachment.Messages.ConversationsId == workspaceConversationId &&
                attachment.Messages.DeletedForEveryoneAt == null)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .Select(attachment => new
            {
                attachment.MessageAttachmentsId,
                attachment.MessagesId,
                attachment.FileName,
                attachment.FileUrl,
                attachment.MimeType,
                attachment.FileExtension,
                attachment.FileSizeBytes,
                attachment.CreatedAt,
                SenderUserId = attachment.Messages.SenderUserId,
            })
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
            return [];

        // 4. Fetch sender info for all uploaders
        var senderIds = attachments
            .Where(a => a.SenderUserId.HasValue)
            .Select(a => a.SenderUserId!.Value)
            .ToHashSet();

        var senders = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => senderIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);

        // 5. Map to response
        return attachments
            .Select(attachment =>
            {
                User? sender = attachment.SenderUserId.HasValue
                    ? senders.GetValueOrDefault(attachment.SenderUserId.Value)
                    : null;

                return new WorkspaceFileResponse(
                    attachment.MessageAttachmentsId,
                    attachment.MessagesId,
                    attachment.FileName,
                    attachment.FileUrl,
                    attachment.MimeType,
                    attachment.FileExtension,
                    attachment.FileSizeBytes,
                    attachment.CreatedAt,
                    attachment.SenderUserId,
                    sender?.FullName,
                    sender?.Avatar);
            })
            .ToList();
    }
}
