using Application.Common.Models;
using Application.Features.Premium.Freelancer.Points.DTOs;
using Application.Features.Premium.Freelancer.Points.Queries;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Premium.Freelancer;

[ApiController]
[Authorize(Roles = nameof(UserRole.Freelancer))]
[Route("api/freelancer/premium")]
public sealed class FreelancerPremiumController : BaseApiController
{
    [HttpGet("points")]
    public async Task<IActionResult> GetPoints(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await Mediator.Send(new GetFreelancerPointsQuery(userId), cancellationToken);
        return Ok(ApiResponse<FreelancerPointsDto>.Ok(result, "Success"));
    }
}
