using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.GetMyFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.GetAllFreelancers.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Freelancer;

[Authorize]
[ApiController]
[Route("api/profile")]
public class FreelancerProfileController : BaseApiController
{
    [HttpPut("freelancer")]
    public async Task<IActionResult> CreateFreelancerProfile([FromBody] CreateFreelancerProfileDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Profile data is required"));
        }

        var command = new CreateFreelancerProfileCommand(dto);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<FreelancerProfileResponseDto>.Ok(result, "Freelancer profile updated successfully"));
    }

    [HttpGet("freelancer/me")]
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

    [HttpGet("freelancer")]
    public async Task<IActionResult> GetAllFreelancers(
        [FromQuery] List<string>? skills, 
        [FromQuery] string? availabilityStatus, 
        [FromQuery] double? minRating)
    {
        var query = new GetAllFreelancersQuery(skills, availabilityStatus, minRating);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<IEnumerable<FreelancerProfileDetailDto>>.Ok(result, "Success"));
    }
}
