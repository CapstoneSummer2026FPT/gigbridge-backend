using Application.Common.Models;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;
using Application.Features.Chat.Common.Messages.GetAround;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Messages.SendWithAttachments.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Common.Models.Files;

namespace Project_API.Controllers.Chat.Common;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : BaseApiController
{
    private const long MaxRequestSizeBytes =
        WorkspaceUploadLimits.MaxTotalFileSizeBytes +
        WorkspaceUploadLimits.MultipartOverheadAllowanceBytes;

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new SendMessageCommand(userId, request));

        return Ok(ApiResponse<MessageResponse>.Ok(result, "Message sent"));
    }

    [HttpPost("attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSizeBytes)]
    public async Task<IActionResult> SendMessageWithAttachments(
        [FromForm] Guid conversationId,
        [FromForm] string clientMessageId,
        [FromForm] string? content,
        [FromForm] List<IFormFile>? attachments)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var streams = new List<Stream>();
        try
        {
            var files = new List<ChatMessageFile>();
            foreach (var file in attachments ?? [])
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                files.Add(new ChatMessageFile(stream, file.FileName, file.ContentType, file.Length));
            }

            var result = await Mediator.Send(new SendMessageWithAttachmentsCommand(
                conversationId,
                userId,
                clientMessageId,
                content,
                files));

            return Ok(ApiResponse<MessageResponse>.Ok(result, "Message sent"));
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [HttpGet("conversation/{conversationId}")]
    public async Task<IActionResult> GetConversationMessages(
        Guid conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int pageSize = 30)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(
            new GetConversationMessagesQuery(conversationId, userId, before, pageSize));

        return Ok(ApiResponse<IReadOnlyList<ConversationMessageResponse>>.Ok(result, "Success"));
    }

    [HttpGet("conversation/{conversationId:guid}/around/{messageId:guid}")]
    public async Task<IActionResult> GetAround(Guid conversationId, Guid messageId, [FromQuery] int radius = 20)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetMessagesAroundQuery(conversationId, messageId, userId, radius));
        return Ok(ApiResponse<IReadOnlyList<ConversationMessageResponse>>.Ok(result, "Success"));
    }
}
