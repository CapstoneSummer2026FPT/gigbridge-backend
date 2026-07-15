using Application.Common.Models;
using Application.Features.JobPosts.Client.CreateDraftJobPost.Commands;
using Application.Features.JobPosts.Client.CreateDraftJobPost.DTOs;
using Application.Features.JobPosts.Client.CreateJobPost.Commands;
using Application.Features.JobPosts.Client.CreateJobPost.DTOs;
using Application.Features.JobPosts.Client.DeleteEmptyDraftJobPost.Commands;
using Application.Features.JobPosts.Client.GetMyDraftJobPosts.Queries;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPostDetail.Queries;
using Application.Features.JobPosts.Client.GetMyJobPosts.DTOs;
using Application.Features.JobPosts.Client.GetMyJobPosts.Queries;
using Application.Features.JobPosts.Client.SaveDraftJobPost.Commands;
using Application.Features.JobPosts.Client.SaveDraftJobPost.DTOs;
using Application.Features.JobPosts.Client.UpdateJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateJobPost.DTOs;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateStatusJobPost.DTOs;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.Commands;
using Application.Features.JobPosts.Client.UpdateVisibilityJobPost.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using Application.Features.JobPosts.Client.GenerateJobDescription.Commands;
using Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;

namespace Project_API.Controllers.Jobs.Client;

[ApiController]
[Route("api/JobPosts")]
[Authorize(Roles = nameof(UserRole.Client))]
public class ClientJobPostsController : BaseApiController
{
    [HttpPost("draft")]
    public async Task<IActionResult> CreateDraftJobPost()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new CreateDraftJobPostCommand(userId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<CreateDraftJobPostResponse>.Ok(result, "Draft job post created successfully"));
    }

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

    [HttpPost("ai/generate")]
    public async Task<IActionResult> GenerateJobDescription([FromBody] GenerateJobDescriptionCommand command)
    {
        if (!TryGetCurrentUserId(out _))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<GenerateJobDescriptionResponse>.Ok(result, "Job description generated successfully"));
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

        return Ok(ApiResponse<IEnumerable<GetMyJobPostDto>>.Ok(result, "Success"));
    }

    [HttpGet("my-drafts")]
    public async Task<IActionResult> GetMyDraftJobPosts()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GetMyDraftJobPostsQuery(userId));

        return Ok(ApiResponse<IEnumerable<GetMyJobPostDto>>.Ok(result, "Success"));
    }

    [HttpGet("my-jobs/{jobPostId}")]
    public async Task<IActionResult> GetMyJobPostDetail(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMyJobPostDetailQuery(userId, jobPostId);
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<GetMyJobPostDetailDto>.Ok(result, "Success"));
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

    [HttpPut("{jobPostId}/draft")]
    public async Task<IActionResult> SaveDraftJobPost(
        Guid jobPostId,
        [FromBody] SaveDraftJobPostRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new SaveDraftJobPostCommand(
            jobPostId,
            userId,
            request);

        var result = await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(result, "Draft job post saved successfully"));
    }

    [HttpDelete("{jobPostId}/draft")]
    public async Task<IActionResult> DeleteEmptyDraftJobPost(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new DeleteEmptyDraftJobPostCommand(jobPostId, userId));

        return Ok(ApiResponse<bool>.Ok(result, "Empty draft job post deleted successfully"));
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
