using Application.Common.Models;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;
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
    public async Task<IActionResult> GetMyJobPosts(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
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

    [HttpPut("{jobPostId}")]
    public async Task<IActionResult> UpdateJobPost(
        Guid jobPostId,
        [FromBody] UpdateJobPostRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateJobPostCommand(
            jobPostId,
            userId,
            request
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Job post updated successfully"));
    }

    [HttpPatch("{jobPostId}/visibility")]
    public async Task<IActionResult> UpdateVisibility(
        Guid jobPostId,
        [FromBody] UpdateVisibilityJobPostRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateVisibilityJobPostCommand(
            jobPostId,
            userId,
            request
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Job post visibility updated successfully"));
    }

    [HttpPatch("{jobPostId}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid jobPostId,
        [FromBody] UpdateStatusJobPostRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UpdateStatusJobPostCommand(
            jobPostId,
            userId,
            request
        );

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Job post status updated successfully"));
    }
}