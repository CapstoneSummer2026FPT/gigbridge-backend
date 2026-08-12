using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Contracts.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.UpdateStatus.Commands;

public sealed class UpdateAdminDisputeStatusCommandHandler :
    IRequestHandler<UpdateAdminDisputeStatusCommand, AdminDisputeDetailResponse>
{
    private static readonly HashSet<(int, int)> AllowedTransitions =
    [
        ((int)DisputeStatus.WaitingAdmin, (int)DisputeStatus.InProgress),
        ((int)DisputeStatus.InProgress,   (int)DisputeStatus.WaitingAdmin),
        ((int)DisputeStatus.Resolved,     (int)DisputeStatus.Closed),
    ];

    private static readonly Dictionary<int, string> StatusLabels = new()
    {
        [(int)DisputeStatus.WaitingAdmin] = "waiting for admin",
        [(int)DisputeStatus.InProgress]   = "in progress",
        [(int)DisputeStatus.Resolved]     = "resolved",
        [(int)DisputeStatus.Closed]       = "closed",
    };

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notifications;
    private readonly ILogger<UpdateAdminDisputeStatusCommandHandler> _logger;

    public UpdateAdminDisputeStatusCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notifications,
        ILogger<UpdateAdminDisputeStatusCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        UpdateAdminDisputeStatusCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(
            _context,
            command.AdminId,
            cancellationToken);

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
                    .ThenInclude(profile => profile.User)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
                    .ThenInclude(profile => profile!.User)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (!AllowedTransitions.Contains((dispute.Status, (int)command.Status)))
        {
            throw new BadRequestException(
                $"Invalid dispute status transition from {dispute.Status} to {(int)command.Status}.");
        }

        var now = _dateTimeService.UtcNow;
        var adminName = await _context.Set<User>()
            .AsNoTracking()
            .Where(u => u.UserId == command.AdminId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        // Auto-assign admin when moving to InProgress
        if (command.Status == DisputeStatus.InProgress)
        {
            dispute.AssignedAdminId ??= command.AdminId;
            dispute.AssignedAt ??= now;

            // Find the dispute conversation and add admin if not already a participant
            var conversation = await _context.Set<Conversation>()
                .Where(c => c.DisputesId == dispute.DisputesId)
                .FirstOrDefaultAsync(cancellationToken);

            if (conversation is not null)
            {
                var alreadyParticipant = await _context.Set<ConversationParticipant>()
                    .AnyAsync(p => p.ConversationsId == conversation.ConversationsId &&
                                   p.UserId == command.AdminId,
                        cancellationToken);

                if (!alreadyParticipant)
                {
                    var participant = new ConversationParticipant
                    {
                        ConversationParticipantId = Guid.NewGuid(),
                        ConversationsId = conversation.ConversationsId,
                        UserId = command.AdminId,
                        ParticipantRole = (int)ParticipantRole.Admin,
                        JoinedAt = now,
                        UnreadCount = 0
                    };
                    _context.Set<ConversationParticipant>().Add(participant);
                }

                // System message for admin assignment
                var systemMessage = ContractConversationEvents.AddSystemMessage(
                    _context,
                    conversation,
                    $"Administrator {(adminName ?? "has been")} assigned to this dispute.",
                    now);

                if (systemMessage is not null)
                {
                    try
                    {
                        await _chatRealtimeNotifier.SendConversationEventAsync(
                            conversation.ConversationsId,
                            "ReceiveMessage",
                            BuildSystemMessagePayload(systemMessage),
                            cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Failed to notify conversation for admin assignment.");
                    }
                }
            }
        }

        dispute.Status = (int)command.Status;
        dispute.UpdatedAt = now;

        // When the dispute is closed, lock the dispute conversation so no party can send
        // messages anymore. If the contract was terminated during resolution, the workspace
        // conversation is locked as well. SendMessageCommandHandler rejects any send against
        // a non-active conversation, so closing the status is the entire enforcement.
        Message? closeSystemMessage = null;
        if (command.Status == DisputeStatus.Closed)
        {
            var conversations = await _context.Set<Conversation>()
                .Where(c => c.ContractsId == dispute.ContractsId)
                .ToListAsync(cancellationToken);

            var contractTerminated = dispute.Contracts.Status == (int)ContractStatus.Cancelled;
            foreach (var conversation in conversations)
            {
                var isDisputeConversation = conversation.DisputesId == dispute.DisputesId;
                var isWorkspaceConversation =
                    conversation.ConversationType == (int)ConversationType.ContractWorkroom &&
                    !conversation.DisputesId.HasValue;

                if (isDisputeConversation || (contractTerminated && isWorkspaceConversation))
                {
                    conversation.Status = (int)ConversationStatus.Closed;
                    conversation.UpdatedAt = now;

                    if (isDisputeConversation)
                    {
                        closeSystemMessage = ContractConversationEvents.AddSystemMessage(
                            _context,
                            conversation,
                            "This dispute has been closed. The conversation is locked.",
                            now);
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (closeSystemMessage is not null)
        {
            try
            {
                await _chatRealtimeNotifier.SendConversationEventAsync(
                    closeSystemMessage.ConversationsId,
                    "ReceiveMessage",
                    BuildSystemMessagePayload(closeSystemMessage),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to notify conversation for dispute closure.");
            }
        }

        // Send notification
        var statusLabel = StatusLabels.GetValueOrDefault(
            (int)command.Status,
            command.Status.ToString());
        await AdminDisputeSupport.NotifyParticipantsAsync(
            _notifications,
            _logger,
            dispute.Contracts,
            dispute,
            $"The dispute on contract '{dispute.Contracts.Title}' is now {statusLabel}.",
            cancellationToken);

        return await AdminDisputeSupport.GetDetailAsync(
            _context,
            dispute.DisputesId,
            cancellationToken);
    }

    private static object BuildSystemMessagePayload(Domain.Entities.Message message)
    {
        return new
        {
            messagesId = message.MessagesId,
            conversationsId = message.ConversationsId,
            senderUserId = (Guid?)null,
            messageType = message.MessageType,
            content = message.Content,
            replyToMessageId = (Guid?)null,
            metadata = (string?)null,
            clientMessageId = (string?)null,
            sentAt = message.SentAt,
            editedAt = (DateTime?)null,
            isDeleted = false,
            attachments = Array.Empty<object>()
        };
    }
}
