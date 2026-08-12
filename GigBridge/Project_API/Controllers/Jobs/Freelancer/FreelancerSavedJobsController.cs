using Application.Common.Models;
using Application.Features.SavedJobs.Freelancer.CheckSavedJob.Queries;
using Application.Features.SavedJobs.Freelancer.GetMySavedJobs.DTOs;
using Application.Features.SavedJobs.Freelancer.GetMySavedJobs.Queries;
using Application.Features.SavedJobs.Freelancer.SaveJob.Commands;
using Application.Features.SavedJobs.Freelancer.UnsaveJob.Commands;
using Domain.Enums.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Jobs.Freelancer;

[ApiController]
[Route("api/SavedJobs")]
[Authorize(Roles = nameof(UserRole.Freelancer))]
public class FreelancerSavedJobsController : BaseApiController
{
    [HttpPost("{jobPostId}")]
    public async Task<IActionResult> SaveJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new SaveJobCommand(userId, jobPostId);
        var result = await Mediator.Send(command);

        return Ok(ApiResponse<Guid>.Ok(result, "Job saved successfully"));
    }

    [HttpDelete("{jobPostId}")]
    public async Task<IActionResult> UnsaveJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var command = new UnsaveJobCommand(userId, jobPostId);
        await Mediator.Send(command);

        return Ok(ApiResponse<bool>.Ok(true, "Job unsaved successfully"));
    }

    [HttpGet("my-saved-jobs")]
    public async Task<IActionResult> GetMySavedJobs(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new GetMySavedJobsQuery(
            userId,
            pageIndex,
            pageSize
        );

        var result = await Mediator.Send(query);

        return Ok(ApiResponse<IEnumerable<SavedJobDto>>.Ok(result, "Success"));
    }

    [HttpGet("{jobPostId}/check")]
    public async Task<IActionResult> CheckSavedJob(Guid jobPostId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var query = new CheckSavedJobQuery(userId, jobPostId);
        var result = await Mediator.Send(query);

        return Ok(ApiResponse<bool>.Ok(result, "Success"));
    }
}