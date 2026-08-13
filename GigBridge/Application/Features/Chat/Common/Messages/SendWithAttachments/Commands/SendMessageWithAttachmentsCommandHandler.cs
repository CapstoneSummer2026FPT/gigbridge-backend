using Application.Common.Exceptions;
using Application.Common.Interfaces.Media;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using MediatR;

namespace Application.Features.Chat.Common.Messages.SendWithAttachments.Commands;

public sealed class SendMessageWithAttachmentsCommandHandler
    : IRequestHandler<SendMessageWithAttachmentsCommand, MessageResponse>
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".bat", ".cmd", ".sh" };

    private const long MaxFileSizeBytes = 100 * 1024 * 1024;
    private const int MaxFilesPerMessage = 5;

    private readonly IMediaService _mediaService;
    private readonly ISender _sender;

    public SendMessageWithAttachmentsCommandHandler(IMediaService mediaService, ISender sender)
    {
        _mediaService = mediaService;
        _sender = sender;
    }

    public async Task<MessageResponse> Handle(
        SendMessageWithAttachmentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Content) && command.Files.Count == 0)
        {
            throw new BadRequestException("Message content or an attachment is required.");
        }

        if (command.Files.Count > MaxFilesPerMessage)
        {
            throw new BadRequestException($"A message may contain at most {MaxFilesPerMessage} attachments.");
        }

        var attachments = new List<SendMessageAttachmentRequest>();

        foreach (var file in command.Files)
        {
            var safeName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeName);

            if (string.IsNullOrWhiteSpace(safeName) ||
                safeName != file.FileName ||
                file.Length <= 0 ||
                file.Length > MaxFileSizeBytes ||
                BlockedExtensions.Contains(extension))
            {
                throw new BadRequestException("An attachment filename, type, or size is invalid.");
            }

            var url = await _mediaService.UploadFileAsync(
                file.Content,
                safeName,
                file.ContentType,
                $"chat/{command.ConversationId}/messages",
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

        return await _sender.Send(
            new SendMessageCommand(
                command.UserId,
                new SendMessageRequest(
                    command.ConversationId,
                    command.ClientMessageId,
                    command.Content,
                    null,
                    attachments)),
            cancellationToken);
    }
}
