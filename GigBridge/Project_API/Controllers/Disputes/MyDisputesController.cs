using Application.Common.Models;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.GetMyDisputes.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Disputes;

[ApiController]
[Route("api/disputes")]
[Authorize]
public sealed class MyDisputesController : BaseApiController
{
    [HttpGet("my")]
    public async Task<IActionResult> GetMyDisputes([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetMyDisputesQuery(userId, page, pageSize));
        return Ok(ApiResponse<MyDisputesResponse>.Ok(result, "Success"));
    }
}
