using Application.Common.Models;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Application.Features.Seo.PublicMarketplace.DTOs;
using Application.Features.Seo.PublicMarketplace.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Seo;

[ApiController]
[AllowAnonymous]
[Route("api/public/job-posts")]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
public sealed class PublicJobPostsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetJobPosts([FromQuery] GetAvailableJobPostsQuery query)
    {
        var result = await Mediator.Send(query);
        var publicResult = result.Select(job => new PublicJobPostSummaryDto(
            job.JobPostsId,
            job.Title,
            job.DescriptionPreview,
            job.MajorName,
            job.CategoryName,
            job.BudgetMin,
            job.BudgetMax,
            job.CreatedAt,
            job.ClientFullName,
            job.SkillNames,
            job.CustomSkillNames));

        return Ok(ApiResponse<IEnumerable<PublicJobPostSummaryDto>>.Ok(publicResult, "Success"));
    }

    [HttpGet("{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetJobPost(Guid id)
    {
        var result = await Mediator.Send(new GetPublicJobPostDetailQuery(id));
        return Ok(ApiResponse<PublicJobPostDetailDto>.Ok(result, "Success"));
    }
}
