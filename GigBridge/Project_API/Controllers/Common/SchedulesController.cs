using Application.Common.Models;
using Application.Features.Chat.Common.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Common;

[ApiController]
[Route("api/schedules")]
[Authorize]
public class SchedulesController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateScheduleRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new CreateScheduleCommand(userId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Schedule created"));
    }

    [HttpGet("{scheduleId:guid}")]
    public async Task<IActionResult> Get(Guid scheduleId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetScheduleQuery(userId, scheduleId));
        return Ok(ApiResponse<ScheduleResponse>.Ok(result, "Success"));
    }

    [HttpGet("conversation/{conversationId:guid}/ongoing")]
    public async Task<IActionResult> GetOngoing(Guid conversationId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetOngoingScheduleQuery(userId, conversationId));
        return Ok(ApiResponse<OngoingScheduleResponse>.Ok(result, "Success"));
    }

    [HttpPut("{scheduleId:guid}")]
    public async Task<IActionResult> Update(Guid scheduleId, UpdateScheduleRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new UpdateScheduleCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Schedule updated"));
    }

    [HttpPost("{scheduleId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid scheduleId, CancelScheduleRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new CancelScheduleCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Schedule cancelled"));
    }
}
