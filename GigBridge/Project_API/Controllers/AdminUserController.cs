using Application.Common.Models;
using Application.Features.Admin.Users.CreateNewUser.Commands;
using Application.Features.Admin.Users.CreateNewUser.DTOs;
using Application.Features.Admin.Users.DeleteUser.Commands;
using Application.Features.Admin.Users.GetAllUser.DTOs;
using Application.Features.Admin.Users.GetAllUser.Queries;
using Application.Features.Admin.Users.GetClientByEmail.Queries;
using Application.Features.Admin.Users.GetFreelancerByEmail.Queries;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Admin.Users.ToggleUserActivity.Commands;
using Application.Features.Admin.Users.UpdateUser.Commands;
using Application.Features.Admin.Users.UpdateUser.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.ontrollers;

[Area("Admin")]
[Route("api/v1/admin/users")]
[Authorize(Roles = "2")]
public class AdminUserController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetAllUsersQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<GetAllUsersResponse>.Ok(result, "Users retrieved successfully"));
    }

    [HttpGet("client-by-email")]
    public async Task<IActionResult> GetClientByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        var user = await Mediator.Send(new GetClientByEmailQuery(email));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("Client not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "Client retrieved successfully"));
    }

    [HttpGet("freelancer-by-email")]
    public async Task<IActionResult> GetFreelancerByEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        var user = await Mediator.Send(new GetFreelancerByEmailQuery(email));

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("Freelancer not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "Freelancer retrieved successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(new CreateNewUserCommand(request));
        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User created successfully"));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserCommand command)
    {
        if (command is null || command.Request is null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body is required"));

        var user = await Mediator.Send(command);

        if (user is null)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<AdminUserDto>.Ok(user, "User updated successfully"));
    }

    [HttpPatch("toggle-activity")]
    public async Task<IActionResult> ToggleActivity([FromBody] ToggleUserActivityCommand command)
    {
        var result = await Mediator.Send(command);

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<object>.NoContent("User activity toggled successfully"));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteUserCommand command)
    {
        var result = await Mediator.Send(command);

        if (!result)
            return NotFound(ApiResponse<object>.NotFound("User not found"));

        return Ok(ApiResponse<object>.NoContent("User deleted successfully"));
    }
}
