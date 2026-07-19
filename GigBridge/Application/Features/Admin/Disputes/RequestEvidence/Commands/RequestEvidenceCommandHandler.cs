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

namespace Application.Features.Admin.Disputes.RequestEvidence.Commands;

public sealed class RequestEvidenceCommandHandler :
    IRequestHandler<RequestEvidenceCommand, AdminDisputeDetailResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;
    private readonly INotificationService _notifications;
    private readonly ILogger<RequestEvidenceCommandHandler> _logger;

    public RequestEvidenceCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier,
        INotificationService notifications,
        ILogger<RequestEvidenceCommandHandler> logger)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AdminDisputeDetailResponse> Handle(
        RequestEvidenceCommand command,
        CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(
            _context,
            command.AdminId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new BadRequestException("Evidence request reason is required.");

        var dispute = await _context.Set<Dispute>()
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.ClientProfiles)
            .Include(item => item.Contracts)
                .ThenInclude(contract => contract.FreelancerProfiles)
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");

        if (dispute.Status != (int)DisputeStatus.UnderReview)
            throw new BadRequestException("Evidence can only be requested for disputes under review.");

        var now = _dateTimeService.UtcNow;
        dispute.Status = (int)DisputeStatus.WaitingEvidence;
        dispute.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        // System message in dispute conversation
        var conversation = await _context.Set<Conversation>()
            .Where(c => c.DisputesId == dispute.DisputesId)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is not null)
        {
            var deadlineText = command.Deadline.HasValue
                ? $" by {command.Deadline.Value:yyyy-MM-dd}"
                : "";
            var systemMessage = ContractConversationEvents.AddSystemMessage(
                _context,
                conversation,
                $"Additional evidence requested: {command.Reason.Trim()}{deadlineText}.",
                now);

            await _context.SaveChangesAsync(cancellationToken);

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
                    _logger.LogWarning(ex, "Failed to notify conversation for evidence request.");
                }
            }
        }

        // Notify participants
        await AdminDisputeSupport.NotifyParticipantsAsync(
            _notifications,
            _logger,
            dispute.Contracts,
            dispute,
            $"Additional evidence requested for dispute on contract '{dispute.Contracts.Title}'.",
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
