using Application.Common.Models;
using Application.Features.Admin.Elo.Commands.ApplyAdminEloAdjustment;
using Application.Features.Admin.Elo.Commands.ResolveEloAppeal;
using Application.Features.Admin.Elo.Commands.UpdateEloPolicy;
using Application.Features.Admin.Elo.DTOs;
using Application.Features.Admin.Elo.Queries.GetAdminEloHistory;
using Application.Features.Admin.Elo.Queries.GetAdminEloUserHistory;
using Application.Features.Admin.Elo.Queries.GetAdminEloUserSummary;
using Application.Features.Admin.Elo.Queries.GetEloAppealDetail;
using Application.Features.Admin.Elo.Queries.GetEloAppeals;
using Application.Features.Admin.Elo.Queries.GetEloPolicy;
using Application.Features.Elo.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Elo.Admin;

public sealed record AdminEloAdjustmentRequest(
    Guid UserId,
    bool Increase,
    EloAdjustmentMode Mode,
    decimal Amount,
    string? Reason,
    Guid? RequestId);

public sealed record AdminResolveEloAppealRequest(
    EloPointAppealStatus Status,
    EloPointAppealResolution Resolution,
    int? CorrectedDelta,
    string? ResolutionNote);

[ApiController]
[Route("api/admin/elo")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminEloController : BaseApiController
{
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? filter = null)
    {
        var result = await Mediator.Send(new GetAdminEloHistoryQuery(page, pageSize, search, filter));
        return Ok(ApiResponse<PaginatedList<AdminEloTransactionRowDto>>.Ok(result, "Elo history retrieved successfully."));
    }

    [HttpGet("users/{userId:guid}/summary")]
    public async Task<IActionResult> GetUserSummary(Guid userId)
    {
        var result = await Mediator.Send(new GetAdminEloUserSummaryQuery(userId));
        return Ok(ApiResponse<AdminEloUserSummaryDto>.Ok(result, "Elo summary retrieved successfully."));
    }

    [HttpGet("users/{userId:guid}/history")]
    public async Task<IActionResult> GetUserHistory(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? filter = null)
    {
        var result = await Mediator.Send(new GetAdminEloUserHistoryQuery(userId, page, pageSize, filter));
        return Ok(ApiResponse<PaginatedList<AdminEloTransactionRowDto>>.Ok(result, "Elo history retrieved successfully."));
    }

    [HttpGet("appeals")]
    public async Task<IActionResult> GetAppeals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null)
    {
        var result = await Mediator.Send(new GetEloAppealsQuery(page, pageSize, status, search));
        return Ok(ApiResponse<PaginatedList<AdminEloAppealRowDto>>.Ok(result, "Elo appeals retrieved successfully."));
    }

    [HttpGet("appeals/{appealId:guid}")]
    public async Task<IActionResult> GetAppealDetail(Guid appealId)
    {
        var result = await Mediator.Send(new GetEloAppealDetailQuery(appealId));
        return Ok(ApiResponse<AdminEloAppealDetailDto>.Ok(result, "Elo appeal detail retrieved successfully."));
    }

    [HttpPost("appeals/{appealId:guid}/resolve")]
    public async Task<IActionResult> ResolveAppeal(Guid appealId, [FromBody] AdminResolveEloAppealRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new ResolveEloAppealCommand(
            adminId,
            appealId,
            request.Status,
            request.Resolution,
            request.CorrectedDelta,
            request.ResolutionNote));

        return Ok(ApiResponse<EloAppealDto>.Ok(result, "Elo appeal resolved successfully."));
    }

    [HttpPost("adjustments")]
    public async Task<IActionResult> ApplyAdjustment([FromBody] AdminEloAdjustmentRequest request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new ApplyAdminEloAdjustmentCommand(
            adminId,
            request.UserId,
            request.Increase,
            request.Mode,
            request.Amount,
            request.Reason,
            request.RequestId ?? Guid.NewGuid()));

        return Ok(ApiResponse<EloTransactionDto?>.Ok(result, "Elo adjustment applied successfully."));
    }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy()
    {
        var result = await Mediator.Send(new GetEloPolicyQuery());
        return Ok(ApiResponse<EloPolicyDto>.Ok(result, "Elo policy retrieved successfully."));
    }

    [HttpPut("policy")]
    public async Task<IActionResult> UpdatePolicy([FromBody] EloPolicyDto request)
    {
        if (!TryGetCurrentUserId(out var adminId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new UpdateEloPolicyCommand(adminId, request.Mode, request.Value));
        return Ok(ApiResponse<EloPolicyDto>.Ok(result, "Elo policy updated successfully."));
    }
}
