using Application.Common.Models;
using Application.Features.Admin.Proposals;
using Application.Features.Admin.Proposals.GetAllProposals.Queries;
using Application.Features.Proposals.Common.DTOs;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/Proposals")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminProposalsController : BaseApiController {
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllProposals([FromQuery] AdminProposalListQuery query) {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PaginatedList<AdminProposalListItem>>.Ok(result, "Success"));
    }
    [HttpGet("admin/{proposalId:guid}")]
    public async Task<IActionResult> GetProposal(Guid proposalId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetAdminProposalAggregateQuery(proposalId));
        return Ok(ApiResponse<AdminProposalDetail>.Ok(result, "Success"));
    }

    [Obsolete("Admin proposal deletion has been replaced by audited invalidation.")]
    [HttpDelete("admin/{proposalId:guid}")]
    public IActionResult DeleteProposal(Guid proposalId) {
        return Conflict(ApiResponse<object>.Conflict("Hard deletion is disabled. Use the invalidate action."));
    }

    [HttpPatch("admin/{proposalId:guid}/invalidate")]
    public async Task<IActionResult> Invalidate(Guid proposalId, [FromBody] ProposalModerationRequest request) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }
        var result = await Mediator.Send(new InvalidateProposalCommand(adminUserId, proposalId, request));
        return Ok(ApiResponse<AdminProposalDetail>.Ok(result, "Proposal invalidated successfully"));
    }

    [HttpPatch("admin/{proposalId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid proposalId, [FromBody] ProposalModerationRequest request) {
        if (!TryGetCurrentUserId(out var adminUserId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new RestoreProposalCommand(adminUserId, proposalId, request));
        return Ok(ApiResponse<AdminProposalDetail>.Ok(result, "Proposal restored successfully"));
    }

    [HttpPost("admin/{proposalId:guid}/internal-notes")]
    public async Task<IActionResult> AddInternalNote(Guid proposalId, [FromBody] AddProposalAdminNoteRequest request) {
        if (!TryGetCurrentUserId(out var adminUserId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new AddProposalAdminNoteCommand(adminUserId, proposalId, request.Content));
        return Ok(ApiResponse<AdminProposalDetail>.Ok(result, "Internal note added"));
    }

    [HttpGet("admin/{proposalId:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(Guid proposalId) {
        var detail = await Mediator.Send(new GetAdminProposalAggregateQuery(proposalId));
        return Ok(ApiResponse<IReadOnlyList<AdminProposalAudit>>.Ok(detail.AuditHistory, "Success"));
    }
}

