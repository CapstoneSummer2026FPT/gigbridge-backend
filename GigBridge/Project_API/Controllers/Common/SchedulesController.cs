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

    [HttpPost("{scheduleId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid scheduleId, ScheduleVersionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new AcceptScheduleCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Schedule accepted"));
    }

    [HttpPost("{scheduleId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid scheduleId, ScheduleVersionRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new RejectScheduleCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Schedule rejected"));
    }

    [HttpPost("{scheduleId:guid}/counterproposal")]
    public async Task<IActionResult> CreateCounterProposal(Guid scheduleId, CounterProposalRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new CreateCounterProposalCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Counterproposal sent"));
    }

    [HttpPut("{scheduleId:guid}/counterproposal")]
    public async Task<IActionResult> UpdateCounterProposal(Guid scheduleId, CounterProposalRequest request)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new UpdateCounterProposalCommand(userId, scheduleId, request));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Counterproposal updated"));
    }

    [HttpPost("{scheduleId:guid}/meeting/retry")]
    public async Task<IActionResult> RetryMeeting(Guid scheduleId)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new RetryScheduleMeetingCommand(userId, scheduleId));
        return Ok(ApiResponse<ScheduleMutationResult>.Ok(result, "Meeting creation retried"));
    }
}
