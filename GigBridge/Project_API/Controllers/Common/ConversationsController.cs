using Application.Common.Models;
using Application.Features.Chat.Common.Conversations.GetMine.DTOs;
using Application.Features.Chat.Common.Conversations.GetMine.Queries;
using Application.Features.Chat.Common.Conversations.MarkAsRead.Commands;
using Application.Features.Chat.Common.Negotiations.OpenFromInvite.Commands;
using Application.Features.Chat.Common.Negotiations.StartFromProposal.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

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
