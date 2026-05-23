using Application.Common.Models;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : BaseApiController
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Mediator.Send(new LoginCommand(request));

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
