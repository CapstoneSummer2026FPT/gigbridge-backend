using Application.Common.Models;
using Application.Features.Proposals.Common.DTOs;
using Application.Features.Proposals.Freelancer.GetMyProposals.Queries;
using Application.Features.Proposals.Freelancer.SubmitProposal.Commands;
using Application.Features.Proposals.Freelancer.SubmitProposal.DTOs;
using Application.Features.Proposals.Freelancer.UpdateProposal.Commands;
using Application.Features.Proposals.Freelancer.UpdateProposal.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Freelancer;

[ApiController]
[Route("api/Proposals")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class FreelancerProposalsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> SubmitProposal([FromBody] SubmitProposalRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new SubmitProposalCommand(request, userId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(result, "Proposal submitted successfully"));
    }

    [HttpGet("my-proposals")]
    public async Task<IActionResult> GetMyProposals([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMyProposalsQuery
        {
            UserId = userId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }
    [HttpPut("{proposalId}")]
    public async Task<IActionResult> UpdateProposal(
    Guid proposalId,
    [FromBody] UpdateProposalRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateProposalCommand(
            proposalId,
            userId,
            request
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Proposal updated successfully"));
    }
}
