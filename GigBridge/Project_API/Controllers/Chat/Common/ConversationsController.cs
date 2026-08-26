using Application.Common.Models;
using Application.Features.Chat.Common.Conversations.GetUnreadCount.DTOs;
using Application.Features.Chat.Common.Conversations.GetUnreadCount.Queries;
using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using Application.Features.Chat.Common.Conversations.GetMine.Queries;
using Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;
using Application.Features.Chat.Common.Conversations.GetInboxStatus.Queries;
using Application.Common.InternalServices.Realtime.Models;
using Application.Features.Chat.Common.Negotiations.OpenFromInvite.Commands;
using Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Chat.Common;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMyConversations()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyConversationsQuery(userId));

        return Ok(ApiResponse<IReadOnlyList<ConversationSummaryResponse>>.Ok(result, "Success"));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryPage(
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 30,
        [FromQuery] Guid? contractId = null,
        [FromQuery] Guid? disputeId = null,
        [FromQuery] Guid? proposalId = null,
        [FromQuery] Guid? jobPostId = null)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetConversationSummaryPageQuery(
            userId, cursor, pageSize, contractId, disputeId, proposalId, jobPostId));
        return Ok(ApiResponse<ConversationSummaryPageResponse>.Ok(result, "Success"));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetConversationUnreadCountQuery(userId));

        return Ok(ApiResponse<ConversationUnreadCountResponse>.Ok(
            result,
            "Unread conversation count retrieved successfully."));
    }

    [HttpGet("{conversationId:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid conversationId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var rows = await Mediator.Send(new GetMyConversationsQuery(
            userId, Take: 1, ConversationId: conversationId));
        var result = rows.SingleOrDefault();
        if (result is null) return NotFound(ApiResponse<object>.Error(404, "Conversation does not exist."));
        return Ok(ApiResponse<ConversationSummaryResponse>.Ok(result, "Success"));
    }

    [HttpGet("inbox-status")]
    public async Task<IActionResult> GetInboxStatus()
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetConversationInboxStatusQuery(userId));
        return Ok(ApiResponse<RealtimeStatusResponse>.Ok(result, "Conversation inbox status retrieved successfully."));
    }

    [HttpPost("proposal/{proposalId}/negotiation")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> StartNegotiationFromProposal(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var conversationId = await Mediator.Send(new StartNegotiationFromProposalCommand(proposalId, userId));

        return Ok(ApiResponse<Guid>.Ok(conversationId, "Negotiation conversation opened"));
    }

    [HttpPost("job/{jobPostId}/freelancers/{freelancerProfileId}/negotiation")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> OpenNegotiationFromInvite(
        Guid jobPostId,
        Guid freelancerProfileId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var conversationId = await Mediator.Send(
            new OpenNegotiationFromInviteCommand(jobPostId, freelancerProfileId, userId));

        return Ok(ApiResponse<Guid>.Ok(conversationId, "Negotiation conversation opened"));
    }

    [HttpPost("{conversationId}/read/{messageId}")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId, Guid messageId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new MarkConversationAsReadCommand(conversationId, messageId, userId));

        return Ok(ApiResponse<bool>.Ok(result, "Conversation marked as read"));
    }
}
