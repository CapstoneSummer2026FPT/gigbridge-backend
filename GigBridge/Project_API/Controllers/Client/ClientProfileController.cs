using System;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.GetClientProfile.DTOs;
using Application.Features.Profiles.ClientProfile.CreateClientProfile.Commands;
using Application.Features.Profiles.ClientProfile.GetClientProfile.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Client;

[Authorize]
[ApiController]
[Route("api/profile")]
public class ClientProfileController : BaseApiController
{
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

    [HttpGet("client/{userId}")]
    public async Task<IActionResult> GetClientProfile(Guid userId)
    {
        var query = new GetClientProfileQuery(userId);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ClientProfileDetailDto>.Ok(result, "Success"));
    }
}
