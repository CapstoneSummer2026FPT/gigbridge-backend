using Application.Common.Models;
using Application.Features.JobPosts.Freelancer.GetMyAppliedJobPosts.Queries;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Freelancer;

[ApiController]
[Route("api/JobPosts")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class FreelancerJobPostsController : BaseApiController
{
    [HttpGet("my-applications")]
    public async Task<IActionResult> GetMyAppliedJobPosts([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMyAppliedJobPostsQuery
        {
            UserId = userId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }
}
