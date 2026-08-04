using Application.Common.Models;
using Application.Features.Profiles.Common.DTOs;
using Application.Features.Profiles.Common.GetMyUserProfile.Queries;
using Application.Features.Profiles.Common.GetUserProfile.Queries;
using Application.Features.Profiles.Common.UpdateUserProfile.Commands;
using Application.Features.Profiles.Common.UpdateUserProfile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Profiles.Common;

[Authorize]
[ApiController]
[Route("api/profile/user")]
public sealed class UserProfileController : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyUserProfile()
    {
        var result = await Mediator.Send(new GetMyUserProfileQuery());
        return Ok(ApiResponse<UserProfileDto>.Ok(result, "Success"));
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserProfile(Guid userId)
    {
        var result = await Mediator.Send(new GetUserProfileQuery(userId));
        return Ok(ApiResponse<UserProfileDto>.Ok(result, "Success"));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateUserProfileDto dto)
    {
        if (dto is null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Profile data is required"));
        }

        var result = await Mediator.Send(new UpdateUserProfileCommand(dto));
        return Ok(ApiResponse<UserProfileDto>.Ok(result, "User profile updated successfully"));
    }
}
