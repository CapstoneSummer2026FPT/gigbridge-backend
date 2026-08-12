using Application.Common.Models;
using Application.Features.Premium.Freelancer.RankProtection.ActivateRankProtection;
using Application.Features.Premium.Freelancer.RankProtection.CancelRankProtection;
using Application.Features.Premium.Freelancer.RankProtection.DTOs;
using Application.Features.Premium.Freelancer.RankProtection.GetRankProtection;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Freelancer;

[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route("api/freelancer/premium/rank-protection")]
public sealed class FreelancerRankProtectionController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new GetRankProtectionQuery(userId), ct);
        return Ok(ApiResponse<RankProtectionDto?>.Ok(result, "Success"));
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateRankProtectionRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new ActivateRankProtectionCommand(userId, request), ct);
        return Ok(ApiResponse<RankProtectionDto>.Ok(result, "Vacation Mode activated"));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(new CancelRankProtectionCommand(userId), ct);
        return Ok(ApiResponse<RankProtectionDto>.Ok(result, "Vacation Mode cancelled"));
    }
}
