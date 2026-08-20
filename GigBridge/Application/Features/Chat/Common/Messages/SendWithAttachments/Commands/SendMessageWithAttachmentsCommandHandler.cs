using Application.Common.Exceptions;
using Application.Common.Interfaces.Files;
using Application.Common.Interfaces.Media;
using Application.Common.Models.Files;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Chat.Common.Messages.SendWithAttachments.Commands;

public sealed class SendMessageWithAttachmentsCommandHandler
    : IRequestHandler<SendMessageWithAttachmentsCommand, MessageResponse>
{
    private readonly IMediaService _mediaService;
    private readonly ISender _sender;
    private readonly IWorkspaceUploadFilePolicy _uploadFilePolicy;
    private readonly ILogger<SendMessageWithAttachmentsCommandHandler>? _logger;

    public SendMessageWithAttachmentsCommandHandler(
        IMediaService mediaService,
        ISender sender,
        IWorkspaceUploadFilePolicy uploadFilePolicy,
        ILogger<SendMessageWithAttachmentsCommandHandler>? logger = null)
    {
        _mediaService = mediaService;
        _sender = sender;
        _uploadFilePolicy = uploadFilePolicy;
        _logger = logger;
    }

    public async Task<MessageResponse> Handle(
        SendMessageWithAttachmentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Content) && command.Files.Count == 0)
        {
            throw new BadRequestException("Message content or an attachment is required.");
        }

        var validatedFiles = await _uploadFilePolicy.ValidateBatchAsync(
            command.Files
                .Select(file => new WorkspaceUploadFile(
                    file.Content,
                    file.FileName,
                    file.ContentType,
                    file.Length))
                .ToList(),
            WorkspaceUploadLimits.MaxFilesPerBatch,
            cancellationToken);

        var folder = $"chat/{command.ConversationId}/messages";
        var attachments = new List<SendMessageAttachmentRequest>(validatedFiles.Count);
        var uploadedUrls = new List<string>(validatedFiles.Count);
        try
        {
            foreach (var file in validatedFiles)
            {
                var url = await _mediaService.UploadFileAsync(
                    file.Content,
                    file.FileName,
                    file.ContentType,
                    folder,
                    cancellationToken);
                uploadedUrls.Add(url);

                attachments.Add(new SendMessageAttachmentRequest(
                    file.FileName,
                    url,
                    "Cloudinary",
                    null,
                    file.ContentType,
                    Path.GetExtension(file.FileName),
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
        catch
        {
            foreach (var url in uploadedUrls)
            {
                try
                {
                    await _mediaService.DeleteFileAsync(url, folder, CancellationToken.None);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger?.LogWarning(
                        exception,
                        "Failed to roll back chat attachment at {FileUrl}.",
                        url);
                }
            }

            throw;
        }
        finally
        {
            await validatedFiles.DisposeAsync();
        }
    }
}
