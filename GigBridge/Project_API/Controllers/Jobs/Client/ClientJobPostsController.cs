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
using Application.Features.Premium.Client.AiJobPostGenerator.Commands;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;
using Application.Features.Premium.Client.JobPostPromotion.Commands;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.DTOs;
using Application.Features.Premium.Client.SmartTalentMatching.GetMatches.Queries;
using Application.Features.Premium.Client.AiInterviews.Create.Commands;
using Application.Features.Premium.Client.AiInterviews.DTOs;
using Application.Features.Premium.Client.AiInterviews.GetResults.Queries;

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
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new GenerateJobDescriptionCommand(userId, command.ClientPrompt));

        return Ok(ApiResponse<GenerateJobDescriptionResponse>.Ok(result, "Job description generated successfully"));
    }

    [HttpPost("{jobPostId:guid}/promote")]
    public async Task<IActionResult> PromoteJobPost(
        Guid jobPostId,
        [FromBody] PromoteJobPostRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new PromoteJobPostCommand(userId, jobPostId, request), cancellationToken);
        return Ok(ApiResponse<JobPostPromotionDto>.Ok(result, "Job post promoted successfully"));
    }

    [HttpGet("{jobPostId:guid}/talent-matches")]
    public async Task<IActionResult> GetTalentMatches(
        Guid jobPostId,
        [FromQuery] int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new GetTalentMatchesQuery(userId, jobPostId, topK), cancellationToken);
        return Ok(ApiResponse<TalentMatchingResultDto>.Ok(result, "Talent recommendation complete"));
    }

    [HttpPost("{jobPostId:guid}/ai-interviews")]
    public async Task<IActionResult> CreateAiInterview(
        Guid jobPostId,
        [FromBody] CreateAiInterviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new CreateAiInterviewCommand(userId, jobPostId, request), cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AiInterviewDefinitionDto>.CreatedAt(result, "AI interview definition created"));
    }

    [HttpGet("{jobPostId:guid}/ai-interviews/{interviewId:guid}/results")]
    public async Task<IActionResult> GetAiInterviewResults(
        Guid jobPostId,
        Guid interviewId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return InvalidTokenResponse();
        var result = await Mediator.Send(
            new GetAiInterviewResultsQuery(userId, jobPostId, interviewId), cancellationToken);
        return Ok(ApiResponse<AiInterviewResultsDto>.Ok(result, "Success"));
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
