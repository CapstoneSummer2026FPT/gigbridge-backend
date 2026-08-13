using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Contracts.Common.GetWorkspaceFiles.DTOs;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Chat;
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
        var messageAttachments = await _context.Set<MessageAttachment>()
            .AsNoTracking()
            .Where(attachment =>
                attachment.Messages.ConversationsId == workspaceConversationId &&
                attachment.Messages.DeletedForEveryoneAt == null)
            .Select(attachment => new WorkspaceFileSource(
                attachment.MessageAttachmentsId,
                attachment.MessagesId,
                attachment.FileName,
                attachment.FileUrl,
                attachment.MimeType,
                attachment.FileExtension,
                attachment.FileSizeBytes,
                attachment.CreatedAt,
                attachment.Messages.SenderUserId,
                0,
                null,
                null,
                null,
                "message",
                null,
                null))
            .ToListAsync(cancellationToken);

        // 4. Fetch product handoffs ("Send Work Materials") for this contract
        var handoffs = await _context.Set<ContractProductHandoff>()
            .AsNoTracking()
            .Where(handoff => handoff.ContractsId == request.ContractId)
            .Select(handoff => new WorkspaceFileSource(
                handoff.ContractProductHandoffId,
                null,
                handoff.FileName ?? handoff.ExternalUrl ?? "Link",
                handoff.FileUrl ?? handoff.ExternalUrl ?? string.Empty,
                handoff.MimeType ?? "application/octet-stream",
                null,
                handoff.FileSizeBytes ?? 0,
                handoff.CreatedAt,
                handoff.SubmittedByUserId,
                handoff.SourceType,
                handoff.ExternalUrl,
                handoff.Note,
                handoff.Version,
                "handoff",
                null,
                null))
            .ToListAsync(cancellationToken);

        // 5. Fetch milestone deliverable attachments for this contract
        var milestoneAttachments = await _context.Set<MilestoneAttachment>()
            .AsNoTracking()
            .Where(attachment => attachment.Milestones.ContractsId == request.ContractId)
            .Select(attachment => new WorkspaceFileSource(
                attachment.MilestoneAttachmentsId,
                null,
                attachment.FileName,
                attachment.FileUrl,
                attachment.MimeType ?? "application/octet-stream",
                null,
                attachment.FileSize ?? 0,
                attachment.CreatedAt,
                attachment.UploadedByUserId,
                0,
                null,
                null,
                null,
                "milestone",
                attachment.MilestonesId,
                attachment.Milestones.Title))
            .ToListAsync(cancellationToken);

        var allFiles = messageAttachments
            .Concat(handoffs)
            .Concat(milestoneAttachments)
            .OrderByDescending(file => file.CreatedAt)
            .ToList();

        if (allFiles.Count == 0)
            return [];

        // 6. Fetch uploader info for all uploaders across every source
        var uploaderIds = allFiles
            .Where(f => f.UploaderUserId.HasValue)
            .Select(f => f.UploaderUserId!.Value)
            .ToHashSet();

        var uploaders = await _context.Set<User>()
            .AsNoTracking()
            .Where(user => uploaderIds.Contains(user.UserId))
            .ToDictionaryAsync(user => user.UserId, cancellationToken);

        // 7. Map to response
        return allFiles
            .Select(file =>
            {
                User? uploader = file.UploaderUserId.HasValue
                    ? uploaders.GetValueOrDefault(file.UploaderUserId.Value)
                    : null;

                return new WorkspaceFileResponse(
                    file.FileId,
                    file.MessageId,
                    file.FileName,
                    file.FileUrl,
                    file.MimeType,
                    file.FileExtension,
                    file.FileSizeBytes,
                    file.CreatedAt,
                    file.UploaderUserId,
                    uploader?.FullName,
                    uploader?.Avatar,
                    file.SourceType,
                    file.ExternalUrl,
                    file.Note,
                    file.Version,
                    file.Context,
                    file.MilestoneId,
                    file.MilestoneTitle);
            })
            .ToList();
    }

    private sealed record WorkspaceFileSource(
        Guid FileId,
        Guid? MessageId,
        string FileName,
        string FileUrl,
        string MimeType,
        string? FileExtension,
        long FileSizeBytes,
        DateTime CreatedAt,
        Guid? UploaderUserId,
        int SourceType,
        string? ExternalUrl,
        string? Note,
        int? Version,
        string Context,
        Guid? MilestoneId,
        string? MilestoneTitle);
}
