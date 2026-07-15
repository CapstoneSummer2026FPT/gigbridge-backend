using Application.Common.Models;
using Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/JobPosts")]
[Authorize(Roles = "Admin")]
public class AdminJobPostsController : BaseApiController {
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllJobPosts([FromQuery] GetAllJobPostsQuery query) {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }
    [HttpGet("admin/{jobPostId:guid}")]
    public async Task<IActionResult> GetJobPost(Guid jobPostId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.JobPosts.GetDetail.Queries.GetAdminJobPostDetailQuery(adminUserId, jobPostId));
        return Ok(ApiResponse<Application.Features.JobPosts.Public.GetJobPostDetail.DTOs.JobPostDetailDto>.Ok(result, "Success"));
    }

    [HttpDelete("admin/{jobPostId:guid}")]
    public async Task<IActionResult> DeleteJobPost(Guid jobPostId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.JobPosts.Delete.Commands.HardDeleteJobPostCommand(adminUserId, jobPostId));
        return Ok(ApiResponse<bool>.Ok(result, "Job post deleted successfully"));
    }

    [HttpPut("admin/{jobPostId:guid}/lock")]
    public async Task<IActionResult> LockJobPost(Guid jobPostId) {
        if (!TryGetCurrentUserId(out var adminUserId)) {
            return InvalidTokenResponse();
        }

        var result = await Mediator.Send(new Application.Features.Admin.JobPosts.ToggleLock.Commands.ToggleJobPostLockCommand(adminUserId, jobPostId));
        return Ok(ApiResponse<bool>.Ok(result, result ? "Job post locked successfully" : "Job post unlocked successfully"));
    }
}

