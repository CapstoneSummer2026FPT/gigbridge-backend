using System.Security.Claims;
using Application.Common.Models;
using Application.Features.Profile.UpdateClientProfile.Commands;
using Application.Features.Profile.UpdateClientProfile.DTOs;
using Application.Features.Profile.UpdateFreelancerProfile.Commands;
using Application.Features.Profile.UpdateFreelancerProfile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProfileController : BaseApiController
{
    [HttpPut("client")]
    public async Task<IActionResult> UpdateClientProfile([FromBody] UpdateClientProfileDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Profile data is required"));
            }

            var command = new UpdateClientProfileCommand(request);
            var result = await Mediator.Send(command);

            return Ok(ApiResponse<object>.Ok(result, "Client profile updated successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Error(401, ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPut("freelancer")]
    public async Task<IActionResult> UpdateFreelancerProfile([FromBody] UpdateFreelancerProfileDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Profile data is required"));
            }

            var command = new UpdateFreelancerProfileCommand(request);
            var result = await Mediator.Send(command);

            return Ok(ApiResponse<object>.Ok(result, "Freelancer profile updated successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Error(401, ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPut("setup-complete")]
    public async Task<IActionResult> MarkSetupComplete()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<object>.Error(401, "User not authenticated"));
            }

            // Endpoint is now just a confirmation - profile handlers already set IsSetup = true
            return Ok(ApiResponse<object>.Ok(null, "Setup marked as complete"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Error(401, ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }
}
