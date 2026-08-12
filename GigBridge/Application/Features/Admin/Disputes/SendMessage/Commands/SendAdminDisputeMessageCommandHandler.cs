using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Admin.Disputes.Common.Internal;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Admin.Disputes.SendMessage.Commands;

public sealed class SendAdminDisputeMessageCommandHandler : IRequestHandler<SendAdminDisputeMessageCommand, MessageResponse>
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".bat", ".cmd", ".sh" };
    private const long MaxFileSize = 100 * 1024 * 1024;

    private readonly IApplicationDbContext _context;
    private readonly IMediaService _media;
    private readonly ISender _sender;
    private readonly INotificationService _notifications;
    private readonly ILogger<SendAdminDisputeMessageCommandHandler> _logger;

    public SendAdminDisputeMessageCommandHandler(
        IApplicationDbContext context,
        IMediaService media,
        ISender sender,
        INotificationService notifications,
        ILogger<SendAdminDisputeMessageCommandHandler> logger)
    {
        _context = context;
        _media = media;
        _sender = sender;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<MessageResponse> Handle(SendAdminDisputeMessageCommand command, CancellationToken cancellationToken)
    {
        await AdminDisputeSupport.EnsureAdminAsync(_context, command.AdminId, cancellationToken);
        var dispute = await _context.Set<Dispute>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DisputesId == command.DisputeId, cancellationToken)
            ?? throw new NotFoundException("Dispute does not exist.");
        if (!Enum.IsDefined(command.Recipient))
            throw new BadRequestException("A valid dispute message recipient is required.");

        var validConversation = await _context.Set<Conversation>().AsNoTracking().AnyAsync(item =>
            item.ConversationsId == command.ConversationId &&
            item.DisputesId == command.DisputeId &&
            item.ConversationType == (int)ConversationType.Dispute &&
            item.DeletedAt == null,
            cancellationToken);
        if (!validConversation)
            throw new BadRequestException("The selected conversation is not this dispute's conversation.");
        if (string.IsNullOrWhiteSpace(command.Content) && command.Files.Count == 0)
            throw new BadRequestException("Message content or an attachment is required.");
        if (command.Files.Count > 5)
            throw new BadRequestException("A message may contain at most five attachments.");

        var attachments = new List<SendMessageAttachmentRequest>();
        foreach (var file in command.Files)
        {
            var safeName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeName);
            if (string.IsNullOrWhiteSpace(safeName) || safeName != file.FileName ||
                file.Length <= 0 || file.Length > MaxFileSize || BlockedExtensions.Contains(extension))
                throw new BadRequestException("An attachment filename, type, or size is invalid.");

            var url = await _media.UploadFileAsync(
                file.Content,
                safeName,
                file.ContentType,
                $"disputes/{command.DisputeId}/messages",
                cancellationToken);
            attachments.Add(new SendMessageAttachmentRequest(
                safeName,
                url,
                "Cloudinary",
                null,
                file.ContentType,
                extension,
                file.Length));
        }

        var mentioned = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(command.Content))
        {
            if (command.Content.Contains("@Reporter", StringComparison.OrdinalIgnoreCase))
                mentioned.Add(dispute.InitiatorId);
            if (command.Content.Contains("@Respondent", StringComparison.OrdinalIgnoreCase) && dispute.RespondentId.HasValue)
                mentioned.Add(dispute.RespondentId.Value);
        }
        mentioned = mentioned.Distinct().Where(userId =>
            command.Recipient == DisputeMessageRecipient.Both ||
            (command.Recipient == DisputeMessageRecipient.Client && userId == dispute.InitiatorId) ||
            (command.Recipient == DisputeMessageRecipient.Freelancer && userId == dispute.RespondentId))
            .ToList();
        var metadata = mentioned.Count == 0 ? null : JsonSerializer.Serialize(new { mentionedUserIds = mentioned });
        var response = await _sender.Send(new SendMessageCommand(
            command.AdminId,
            new SendMessageRequest(
                command.ConversationId,
                Guid.NewGuid().ToString("N"),
                command.Content,
                null,
                attachments),
            metadata,
            command.Recipient), cancellationToken);

        foreach (var userId in mentioned)
        {
            try
            {
                await _notifications.CreateNotificationAsync(
                    userId,
                    NotificationType.DisputeUpdate,
                    "Administrator mentioned you",
                    "An administrator mentioned you in the dispute conversation.",
                    dispute.DisputesId,
                    nameof(Dispute),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Official dispute message persisted but mention notification delivery failed.");
            }
        }

        return response;
    }
}
