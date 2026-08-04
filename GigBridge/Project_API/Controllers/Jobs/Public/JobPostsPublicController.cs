using Application.Common.Models;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.Queries;
using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
using Application.Features.JobPosts.Public.SearchAvailableJobPosts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Project_API.Security;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Jobs.Public;

[ApiController]
[Route("api/JobPosts")]
[AllowAnonymous]
public class JobPostsPublicController : BaseApiController
{
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicJobPosts([FromQuery] GetAvailableJobPostsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }

    [HttpGet("search")]
    [EnableRateLimiting(AuthRateLimitPolicies.DiscoveryAnalytics)]
    public async Task<IActionResult> SearchJobPosts(
        [FromQuery] SearchAvailableJobPostsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SearchAvailableJobPostsCommand(
            request.PageIndex, request.PageSize, request.Search, request.SkillIds,
            request.BudgetMin, request.BudgetMax, request.SortBy, request.SortDesc,
            request.Category, request.Skills, request.WorkType, request.PostedWithinDays,
            request.AiOnly, request.SearchEventId, ResolveAnalyticsActor());
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<PagedJobSearchResponse>.Ok(result, "Success"));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJobPostDetail(Guid id)
    {
        var query = new GetJobPostDetailQuery(id);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<JobPostDetailDto>.Ok(result, "Success"));
    }

    private string ResolveAnalyticsActor()
    {
        if (TryGetCurrentUserId(out var userId)) return $"user:{userId:N}";
        var session = Request.Headers["X-Analytics-Session"].ToString();
        return Guid.TryParse(session, out var sessionId) ? $"session:{sessionId:N}" : string.Empty;
    }
}
