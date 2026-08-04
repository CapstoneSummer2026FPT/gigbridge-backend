using System;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.Common.DTOs;
using Application.Features.Profiles.ClientProfile.GetClientProfile.Queries;
using Application.Features.Profiles.ClientProfile.GetMyClientProfile.Queries;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.Commands;
using Application.Features.Profiles.ClientProfile.UpdateClientProfile.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using Domain.Enums;

namespace Project_API.Controllers.Profiles.Client;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ClientProfileController : BaseApiController
{
    [HttpGet("client/me")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> GetMyClientProfile()
    {
        var query = new GetMyClientProfileQuery();
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ClientProfileDetailDto>.Ok(result, "Success"));
    }

    [HttpPut("client")]
    [Authorize(Roles = nameof(UserRole.Client))]
    public async Task<IActionResult> UpdateClientProfile([FromBody] UpdateClientProfileDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Profile data is required"));
        }

        var command = new UpdateClientProfileCommand(dto);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<ClientProfileResponseDto>.Ok(result, "Client profile updated successfully"));
    }

    [HttpGet("client/{userId}")]
    public async Task<IActionResult> GetClientProfile(Guid userId)
    {
        var query = new GetClientProfileQuery(userId);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ClientProfileDetailDto>.Ok(result, "Success"));
    }
}
