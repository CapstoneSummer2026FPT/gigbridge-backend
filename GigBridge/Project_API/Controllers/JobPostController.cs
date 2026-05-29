using Application.Common.Models;
using Application.DTOs.Admin;
using Application.Features.JobPosts.CreateJobPost.Commands;
using Application.Features.JobPosts.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.GetJobPostDetail.Queries;
using Application.Features.JobPosts.GetMyAppliedJobPosts.Queries;
using Application.Features.JobPosts.GetMyJobPosts.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Project_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobPostsController : BaseApiController
{
    // ==================== PUBLIC ENDPOINTS (Không cần login) ====================

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicJobPosts([FromQuery] GetAvailableJobPostsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetJobPostDetail(Guid id)
    {
        var query = new GetJobPostDetailQuery(id);        // ← Sửa lỗi CS7036
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<JobPostDetailDto>.Ok(result));
    }

    // ==================== AUTHENTICATED ENDPOINTS ====================

    [HttpPost]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> CreateJobPost([FromBody] CreateJobPostCommand command)
    {
        var clientId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(clientId))
            return Unauthorized(ApiResponse<object>.Error(401, "Invalid token"));

        // Sửa lỗi CS8852 - init-only property
        command = command with { ClientProfilesId = Guid.Parse(clientId) };

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<Guid>.Ok(result, "Job post created successfully"));
    }

    // Admin xem tất cả JobPosts
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllJobPosts([FromQuery] GetAvailableJobPostsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }

    // Client xem JobPosts của chính mình
    [HttpGet("my-jobs")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> GetMyJobPosts([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var clientId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        var query = new GetMyJobPostsQuery
        {
            ClientProfilesId = Guid.Parse(clientId!),
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }

    // Freelancer xem các JobPost mình đã apply
    [HttpGet("my-applications")]
    [Authorize(Roles = "Freelancer")]
    public async Task<IActionResult> GetMyAppliedJobPosts([FromQuery] int pageIndex = 1, int pageSize = 10)
    {
        var freelancerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value;

        var query = new GetMyAppliedJobPostsQuery
        {
            FreelancerProfilesId = Guid.Parse(freelancerId!),
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }
}