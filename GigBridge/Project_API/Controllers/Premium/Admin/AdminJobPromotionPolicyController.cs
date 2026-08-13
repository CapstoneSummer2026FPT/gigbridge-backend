using Application.Common.Models;
using Application.Features.Premium.Client.JobPostPromotion.Commands;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Application.Features.Premium.Client.JobPostPromotion.Queries;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Premium.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/job-promotion-policy")]
public sealed class AdminJobPromotionPolicyController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetJobPromotionPolicyQuery(), cancellationToken);
        return Ok(ApiResponse<JobPromotionPolicyDto>.Ok(result, "Success"));
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateJobPromotionPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var adminUserId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new UpdateJobPromotionPolicyCommand(adminUserId, request), cancellationToken);
        return Ok(ApiResponse<JobPromotionPolicyDto>.Ok(result, "Job promotion policy updated successfully"));
    }
}
