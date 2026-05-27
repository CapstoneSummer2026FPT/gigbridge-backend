using Application.Common.Models;
using Application.Features.Auth.Command;
using Application.Features.Auth.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : BaseApiController
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);

        if (result == null)
        {
            return Unauthorized(ApiResponse<object>.Error(401, "Invalid credentials"));
        }

        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful"));
    }

    [HttpGet("test-auth")]
    [Authorize]
    public IActionResult TestAuth()
    {
        var data = new { message = "You are authorized!", user = User.Identity?.Name };
        return Ok(ApiResponse<object>.Ok(data, "Authorization verified"));
    }
}
