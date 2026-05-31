using Application.Common.Models;
using Application.Features.Proposals.DTOs;
using Application.Features.Proposals.GetAllProposals.Queries;
using Application.Features.Proposals.GetMyProposals.Queries;
using Application.Features.Proposals.GetProposalsByJobPost.Queries;
using Application.Features.Proposals.SubmitProposal.Commands;
using Application.Features.Proposals.SubmitProposal.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Project_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProposalsController : BaseApiController
{
    // ==================== FREELANCER ENDPOINTS ====================

    [HttpPost]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> SubmitProposal([FromBody] SubmitProposalRequest request)
    {
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdValue))
            return Unauthorized(ApiResponse<object>.Error(401, "Invalid token"));

        var command = new SubmitProposalCommand(
            Request: request,
            UserId: Guid.Parse(userIdValue)
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(result, "Proposal submitted successfully"));
    }

    [HttpGet("my-proposals")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetMyProposals([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdValue))
            return Unauthorized(ApiResponse<object>.Error(401, "Invalid token"));

        var query = new GetMyProposalsQuery
        {
            UserId = Guid.Parse(userIdValue),
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }

    // ==================== CLIENT ENDPOINTS ====================

    [HttpGet("job/{jobPostId}/proposals")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetProposalsByJobPost(Guid jobPostId, [FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var clientId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(clientId))
            return Unauthorized(ApiResponse<object>.Error(401, "Invalid token"));

        var query = new GetProposalsByJobPostQuery
        {
            JobPostsId = jobPostId,
            UserId = Guid.Parse(clientId!),
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }

    // ==================== ADMIN ENDPOINTS ====================

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllProposals([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var query = new GetAllProposalsQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }
}