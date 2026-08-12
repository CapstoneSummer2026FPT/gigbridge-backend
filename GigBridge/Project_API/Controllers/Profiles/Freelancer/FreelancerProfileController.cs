using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancers.Queries;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.GetMyFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using Domain.Enums.Accounts;

namespace Project_API.Controllers.Profiles.Freelancer;

[Authorize]
[ApiController]
[Route("api/profile")]
public class FreelancerProfileController : BaseApiController
{
    [HttpPut("freelancer")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> UpdateFreelancerProfile([FromBody] UpdateFreelancerProfileDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Profile data is required"));
        }

        var command = new UpdateFreelancerProfileCommand(dto);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<FreelancerProfileResponseDto>.Ok(result, "Freelancer profile updated successfully"));
    }

    [HttpGet("freelancer/me")]
    [Authorize(Roles = nameof(UserRole.Freelancer))]
    public async Task<IActionResult> GetMyFreelancerProfile()
    {
        var query = new GetMyFreelancerProfileQuery();
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<FreelancerProfileDetailDto>.Ok(result, "Success"));
    }

    [HttpGet("freelancer/{userId}")]
    public async Task<IActionResult> GetFreelancerProfile(Guid userId)
    {
        var query = new GetFreelancerProfileQuery(userId);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<FreelancerProfileDetailDto>.Ok(result, "Success"));
    }

    [HttpGet("freelancers")]
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
        return Ok(ApiResponse<PaginatedList<FreelancerSummaryDto>>.Ok(result, "Success"));
    }
}
