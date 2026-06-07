using Application.Common.Models;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.Public.GetJobPostDetail.DTOs;
using Application.Features.JobPosts.Public.GetJobPostDetail.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Public;

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJobPostDetail(Guid id)
    {
        var query = new GetJobPostDetailQuery(id);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<JobPostDetailDto>.Ok(result, "Success"));
    }
}
