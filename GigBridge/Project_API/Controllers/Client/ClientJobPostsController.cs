using Application.Common.Models;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Client;

[ApiController]
[Route("api/JobPosts")]
[Authorize(Roles = nameof(UserRole.Client))]
public class ClientJobPostsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateJobPost([FromBody] CreateJobPostRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateJobPostCommand(request, userId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(result, "Job post created successfully"));
    }

    [HttpGet("my-jobs")]
    public async Task<IActionResult> GetMyJobPosts([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMyJobPostsQuery
        {
            UserId = userId,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }
}
