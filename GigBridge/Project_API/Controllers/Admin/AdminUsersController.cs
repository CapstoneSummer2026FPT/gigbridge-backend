using Application.Common.Models;
using Application.DTOs.Admin;
using Application.Features.Admin.Users.Command;
using Application.Features.Admin.Users.Dto;
using Infrastructure.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Project_API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController : AdminControllerBase
{
    private readonly IAdminUserService _userService;

    public AdminUsersController(IAdminUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] UserPageQueryDto query, CancellationToken cancellationToken)
    {
        var data = await _userService.GetAllAsync(query, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "Users retrieved successfully"));
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var data = await _userService.GetAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "User retrieved successfully"));
    }

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid userId, [FromBody] UserStatusRequestDto request, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new ChangeUserStatusCommand(userId, request, GetActor()), cancellationToken);
        return Ok(ApiResponse<object>.Ok(data, "User status updated successfully"));
    }
}
