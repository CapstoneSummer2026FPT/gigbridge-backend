using Application.Common.Models;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.DownloadEvidence.Queries;
using Application.Features.Admin.Disputes.GetDetail.Queries;
using Application.Features.Admin.Disputes.GetList.Queries;
using Application.Features.Admin.Disputes.Resolve.Commands;
using Application.Features.Admin.Disputes.UpdateStatus.Commands;
using Application.Features.Disputes.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin.Disputes;

public sealed record AdminDisputeStatusRequest(DisputeStatus Status);

public sealed record AdminResolveDisputeRequest(
    DisputeResolution Resolution,
    string ResolutionNote);

[ApiController]
[Route("api/admin/disputes")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminDisputesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DisputeStatus? status = null,
        [FromQuery] string? search = null)
    {
        var result = await Mediator.Send(new GetAdminDisputesQuery(
            page,
            pageSize,
            status,
            search));

        return Ok(ApiResponse<AdminDisputeListResponse>.Ok(result, "Disputes retrieved successfully."));
    }

    [HttpGet("{disputeId:guid}")]
    public async Task<IActionResult> GetById(Guid disputeId)
    {
        var result = await Mediator.Send(new GetAdminDisputeDetailQuery(disputeId));
        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute retrieved successfully."));
    }

    [HttpGet("{disputeId:guid}/evidence/{evidenceId:guid}/download")]
    public async Task<IActionResult> DownloadEvidence(Guid disputeId, Guid evidenceId)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetAdminDisputeEvidenceDownloadQuery(
            disputeId,
            evidenceId,
            adminId));

        return Ok(ApiResponse<DisputeEvidenceDownloadResponse>.Ok(result, "Success"));
    }

    [HttpPatch("{disputeId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid disputeId,
        [FromBody] AdminDisputeStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new UpdateAdminDisputeStatusCommand(
            disputeId,
            adminId,
            request.Status));

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute status updated successfully."));
    }

    [HttpPost("{disputeId:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid disputeId,
        [FromBody] AdminResolveDisputeRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new ResolveAdminDisputeCommand(
            disputeId,
            adminId,
            request.Resolution,
            request.ResolutionNote));

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute resolved successfully."));
    }
}
