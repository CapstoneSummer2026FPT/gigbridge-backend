using Application.Common.Models;
using Application.Features.Admin.Proposals.GetAllProposals.Queries;
using Application.Features.Proposals.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/Proposals")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminProposalsController : BaseApiController {
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllProposals([FromQuery] int pageIndex = 1, int pageSize = 10) {
        var query = new GetAllProposalsQuery {
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<ProposalDto>>.Ok(result, "Success"));
    }
    [HttpGet("admin/{proposalId:guid}")]
    public async Task<IActionResult> GetProposal(Guid proposalId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.Proposals.GetDetail.Queries.GetAdminProposalDetailQuery(adminUserId, proposalId));
        return Ok(ApiResponse<ProposalDto>.Ok(result, "Success"));
    }

    [HttpDelete("admin/{proposalId:guid}")]
    public async Task<IActionResult> DeleteProposal(Guid proposalId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.Proposals.Delete.Commands.HardDeleteProposalCommand(adminUserId, proposalId));
        return Ok(ApiResponse<bool>.Ok(result, "Proposal deleted successfully"));
    }
}

