using Application.DTOs.Admin;
using Application.Common.Models;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/job-posts")]
public sealed class AdminJobPostsController : AdminControllerBase
{
    private readonly IAdminJobPostService _jobPostService;

    public AdminJobPostsController(IAdminJobPostService jobPostService)
    {
        _jobPostService = jobPostService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _jobPostService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Job posts retrieved successfully"));
    }

    [HttpGet("{jobPostId:guid}")]
    public async Task<IActionResult> Get(Guid jobPostId, CancellationToken cancellationToken)
    {
        var data = await _jobPostService.GetAsync(jobPostId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Job post retrieved successfully"));
    }

    [HttpPatch("{jobPostId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid jobPostId, [FromBody] JobStatusRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _jobPostService.CancelAsync(jobPostId, request, GetActor(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Job post cancelled successfully"));
    }
}
