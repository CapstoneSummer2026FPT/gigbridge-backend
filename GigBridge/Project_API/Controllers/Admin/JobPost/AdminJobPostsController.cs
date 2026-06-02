using Application.Common.Models;
using Application.Features.JobPosts.Admin.GetAllJobPosts.Queries;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/JobPosts")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminJobPostsController : BaseApiController
{
    [HttpGet("admin/all")]
    public async Task<IActionResult> GetAllJobPosts([FromQuery] GetAllJobPostsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<JobPostSummaryDto>>.Ok(result, "Success"));
    }
}
