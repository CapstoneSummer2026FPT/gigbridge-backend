using Application.Common.Models;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Common.GetProposalDetail.Queries;
using Application.Features.Proposals.Common.AcceptForNegotiation.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands;
using Application.Features.Proposals.Common.UpdateProposalStatus.Commands.DTOs;
using Application.Features.Proposals.Freelancer.GetMyProposalByJobPost.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.Proposals;

[ApiController]
[Route("api/Proposals")]
[Authorize]
public class ProposalsController : BaseApiController
{
    [HttpGet("{proposalId}")]
    public async Task<IActionResult> GetProposalDetail(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetProposalDetailQuery(proposalId, userId);
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<ProposalDetailDto>.Ok(result, "Success"));
    }
    [HttpGet("job/{jobPostId}/my-proposal")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetMyProposalByJobPost(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMyProposalByJobPostQuery(jobPostId, userId);
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<ProposalDetailDto>.Ok(result, "Success"));
    }
    [HttpPatch("{proposalId}/status")]
    public async Task<IActionResult> UpdateProposalStatus(
    Guid proposalId,
    [FromBody] UpdateProposalStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateProposalStatusCommand(
            proposalId,
            userId,
            request
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<UpdateProposalStatusResponse>.Ok(result, "Proposal status updated successfully"));
    }

    [HttpPost("{proposalId}/accept-for-negotiation")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> AcceptProposalForNegotiation(Guid proposalId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new AcceptProposalForNegotiationCommand(proposalId, userId);
        var conversationId = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(conversationId, "Proposal accepted for negotiation"));
    }
}
