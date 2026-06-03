using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Common.Exceptions;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Application.Features.Profiles.FreelancerProfile.CreateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.GetMyFreelancerProfile.Queries;
using Application.Features.Profiles.FreelancerProfile.GetAllFreelancers.Queries;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.Commands;
using Application.Features.Profiles.ClientProfile.GetClientProfile.Queries;
using Domain.Entities;
using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[Authorize]
public class ProfileController : BaseApiController
{
    private readonly IApplicationDbContext _context;

    public ProfileController(IApplicationDbContext context)
    {
        _context = context;
    }

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

    [HttpPut("client")]
    public async Task<IActionResult> CreateClientProfile([FromBody] CreateClientProfileDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Profile data is required"));
        }

        var command = new CreateClientProfileCommand(dto);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<ClientProfileResponseDto>.Ok(result, "Client profile updated successfully"));
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

    [HttpGet("client/{userId}")]
    public async Task<IActionResult> GetClientProfile(Guid userId)
    {
        var query = new GetClientProfileQuery(userId);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ClientProfileDetailDto>.Ok(result, "Success"));
    }

    [HttpPut("setup-complete")]
    public async Task<IActionResult> MarkSetupComplete()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return InvalidTokenResponse();
        }

        var user = await _context.Set<User>().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        user.IsSetup = true;
        await _context.SaveChangesAsync(default);

        return Ok(ApiResponse<object?>.Ok(null, "Setup marked as complete"));
    }

    [HttpGet("company-sizes")]
    public IActionResult GetCompanySizes()
    {
        var companySizes = new[]
        {
            new { Id = 0, Name = "Solo (1-9 employees)" },
            new { Id = 1, Name = "Small (10-49 employees)" },
            new { Id = 2, Name = "Medium (50-249 employees)" },
            new { Id = 3, Name = "Large (250+ employees)" }
        };
        return Ok(ApiResponse<object>.Ok(companySizes, "Success"));
    }

    [HttpGet("industries")]
    public IActionResult GetIndustries()
    {
        var industries = new[]
        {
            "Technology", "Finance", "Healthcare", "E-commerce", "Education",
            "Marketing", "Real Estate", "Entertainment", "Manufacturing", "Other"
        };
        return Ok(ApiResponse<IEnumerable<string>>.Ok(industries, "Success"));
    }
}
