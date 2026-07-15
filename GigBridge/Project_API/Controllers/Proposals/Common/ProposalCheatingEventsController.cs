using Application.Common.Models;
using Application.Features.Proposals.Freelancer.Cheating.Commands;
using Application.Features.Proposals.Freelancer.Cheating.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Common.Proposals;

[ApiController]
[Route("api/Proposals/{proposalId:guid}/cheating-events")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class ProposalCheatingEventsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> LogEvent(
        Guid proposalId,
        [FromBody] LogProposalCheatingEventRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new LogProposalCheatingEventCommand(
            proposalId,
            userId,
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<CheatingEventLogResponse>.Ok(result, "Cheating event logged successfully"));
    }
}
