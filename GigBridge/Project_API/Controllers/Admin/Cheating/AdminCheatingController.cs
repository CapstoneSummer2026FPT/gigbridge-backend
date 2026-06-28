using Application.Common.Models;
using Application.Features.Admin.Cheating.DTOs;
using Application.Features.Admin.Cheating.GetEvents.Queries;
using Application.Features.Admin.Cheating.GetViolationDetail.Queries;
using Application.Features.Admin.Cheating.GetViolations.Queries;
using Application.Features.Admin.Cheating.ReviewViolation.Commands;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin.Cheating;

[ApiController]
[Route("api/admin/cheating")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminCheatingController : BaseApiController
{
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] GetAdminCheatingEventsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<AdminCheatingEventsResponse>.Ok(result, "Cheating events retrieved successfully"));
    }

    [HttpGet("violations")]
    public async Task<IActionResult> GetViolations([FromQuery] GetAdminCheatingViolationsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<AdminCheatingViolationsResponse>.Ok(result, "Cheating violations retrieved successfully"));
    }

    [HttpGet("violations/{violationId:guid}")]
    public async Task<IActionResult> GetViolationDetail(Guid violationId)
    {
        var result = await Mediator.Send(new GetAdminCheatingViolationDetailQuery(violationId));
        return Ok(ApiResponse<AdminCheatingViolationDetailDto>.Ok(result, "Cheating violation retrieved successfully"));
    }

    [HttpPatch("violations/{violationId:guid}/review")]
    public async Task<IActionResult> ReviewViolation(
        Guid violationId,
        [FromBody] ReviewCheatingViolationRequest request)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new ReviewCheatingViolationCommand(violationId, adminUserId, request));
        return Ok(ApiResponse<AdminCheatingViolationDto>.Ok(result, "Cheating violation review updated successfully"));
    }
}
