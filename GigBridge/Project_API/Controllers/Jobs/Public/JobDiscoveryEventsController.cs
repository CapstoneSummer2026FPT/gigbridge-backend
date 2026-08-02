using System.Security.Claims;
using Application.Common.Models;
using Application.Features.JobPosts.Public.RecordDiscoveryEvent.Commands;
using Application.Features.JobPosts.Public.RecordDiscoveryEvent.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_API.Controllers.Common;
using Project_API.Security;

namespace Project_API.Controllers.Jobs.Public;

[ApiController]
[Route("api/job-discovery/events")]
[AllowAnonymous]
[EnableRateLimiting(AuthRateLimitPolicies.DiscoveryAnalytics)]
public sealed class JobDiscoveryEventsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Record(JobDiscoveryEventRequest request, CancellationToken cancellationToken)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var session = Request.Headers["X-Analytics-Session"].ToString();
        var actor = Guid.TryParse(user, out var userId)
            ? $"user:{userId:N}"
            : Guid.TryParse(session, out var sessionId) ? $"session:{sessionId:N}" : string.Empty;
        var result = await Mediator.Send(new RecordJobDiscoveryEventCommand(
            actor, request.EventId, request.JobPostId, request.SearchEventId), cancellationToken);
        var message = result.Accepted
            ? "Job discovery event accepted."
            : "Browsing was not interrupted; analytics capture will be unavailable for this event.";
        return Accepted(ApiResponse<RecordJobDiscoveryEventResult>.Ok(result, message));
    }
}
