using Application.Common.Models;
using Application.Features.Chat.Common.Messages.GetConversationMessages.DTOs;
using Application.Features.Chat.Common.Messages.GetConversationMessages.Queries;
using Application.Features.Chat.Common.Messages.Send.Commands;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : BaseApiController
{
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
}
