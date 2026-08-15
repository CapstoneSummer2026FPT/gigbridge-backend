using Application.Common.Models;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;
using Application.Features.Seo.PublicMarketplace.DTOs;
using Application.Features.Seo.PublicMarketplace.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Seo;

[ApiController]
[AllowAnonymous]
[Route("api/public/freelancers")]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
public sealed class PublicFreelancersController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetFreelancers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] List<string>? skills = null,
        [FromQuery] string? availabilityStatus = null,
        [FromQuery] double? minRating = null,
        [FromQuery] string? sort = null)
    {
        var query = new GetFreelancersQuery(
            page,
            pageSize,
            search,
            skills,
            availabilityStatus,
            minRating,
            sort);
        var result = await Mediator.Send(query);
        var publicItems = result.Items.Select(profile => new PublicFreelancerSummaryDto(
            profile.UserId,
            profile.UserFullName,
            profile.UserAvatar,
            profile.Title,
            profile.Bio,
            profile.Location,
            profile.MajorName,
            profile.Rating,
            profile.UpdatedAt,
            profile.Skills
                .Select(skill => new PublicFreelancerSkillDto(skill.SkillName))
                .ToList())).ToList();
        var publicResult = new PaginatedList<PublicFreelancerSummaryDto>(
            publicItems,
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return Ok(ApiResponse<PaginatedList<PublicFreelancerSummaryDto>>.Ok(publicResult, "Success"));
    }

    [HttpGet("{userId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetFreelancer(Guid userId)
    {
        var result = await Mediator.Send(new GetPublicFreelancerProfileQuery(userId));
        return Ok(ApiResponse<PublicFreelancerProfileDto>.Ok(result, "Success"));
    }
}
