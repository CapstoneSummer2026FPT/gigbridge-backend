using Application.Common.Models;
using Application.Features.Admin.Disputes.Common.DTOs;
using Application.Features.Admin.Disputes.DownloadEvidence.Queries;
using Application.Features.Admin.Disputes.GetDetail.Queries;
using Application.Features.Admin.Disputes.GetList.Queries;
using Application.Features.Admin.Disputes.RequestEvidence.Commands;
using Application.Features.Admin.Disputes.Resolve.Commands;
using Application.Features.Admin.Disputes.UpdateStatus.Commands;
using Application.Features.Disputes.Common.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin.Disputes;

public sealed record AdminDisputeStatusRequest(DisputeStatus Status);

public sealed record AdminMilestoneActionRequest(Guid MilestoneId, int Action);

public sealed record AdminResolveDisputeRequest(
    DisputeResolution Resolution,
    string ResolutionNote,
    string? InternalNotes,
    decimal? RefundToClientAmount,
    decimal? ReleaseToFreelancerAmount,
    List<AdminMilestoneActionRequest>? MilestoneActions,
    int ContractAction);

public sealed record AdminRequestEvidenceRequest(
    string Reason,
    DateTime? Deadline);

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

    [HttpPost("{disputeId:guid}/request-evidence")]
    public async Task<IActionResult> RequestEvidence(
        Guid disputeId,
        [FromBody] AdminRequestEvidenceRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new Application.Common.Exceptions.BadRequestException("Evidence request reason is required.");

        var result = await Mediator.Send(new RequestEvidenceCommand(
            disputeId,
            adminId,
            request.Reason,
            request.Deadline));

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Evidence requested successfully."));
    }

    [HttpPost("{disputeId:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid disputeId,
        [FromBody] AdminResolveDisputeRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var command = new ResolveAdminDisputeCommand(
            disputeId,
            adminId,
            request.Resolution,
            request.ResolutionNote,
            request.InternalNotes,
            request.RefundToClientAmount,
            request.ReleaseToFreelancerAmount,
            request.MilestoneActions?.Select(m => new AdminMilestoneAction(m.MilestoneId, m.Action)).ToList(),
            (AdminContractAction)request.ContractAction);

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<AdminDisputeDetailResponse>.Ok(result, "Dispute resolved successfully."));
    }
}
