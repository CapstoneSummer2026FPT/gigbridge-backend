using Application.Common.Models;
using Application.Features.Auth.Commands.ForgotPassword;
using Application.Features.Auth.Commands.GoogleLogin;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.RefreshToken;
using Application.Features.Auth.Commands.Register;
using Application.Features.Auth.Commands.ResendEmail;
using Application.Features.Auth.Commands.ResetPassword;
using Application.Features.Auth.Commands.ValidateToken;
using Application.Features.Auth.Commands.VerifyEmail;
using Application.Features.Auth.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;
using System;
using System.Threading.Tasks;

namespace Project_API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : BaseApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Registration data is required"));
            }

            var user = await Mediator.Send(new RegisterCommand(request));

            if (user == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Registration failed"));
            }

            return Ok(ApiResponse<UserDTO>.Ok(user, "User registered successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Login data is required"));
            }

            var (loginData, refreshToken) = await Mediator.Send(new LoginWithRefreshCommand(request));

            if (loginData == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Login failed"));
            }

            SetRefreshTokenCookie(refreshToken);

            return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Login successful"));
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

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] string authCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(authCode))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Authorization code is required"));
            }

            var (loginData, refreshToken) = await Mediator.Send(new GoogleLoginCommand(authCode));

            if (loginData == null)
            {
                return BadRequest(ApiResponse<object>.Error(400, "Google login failed"));
            }

            SetRefreshTokenCookie(refreshToken);

            return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Login successful"));
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

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest request)
    {
        try
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(ApiResponse<object>.Error(401, "Refresh token is missing. Please log in again."));
            }

            var (loginData, newRefreshToken) = await Mediator.Send(new RefreshTokenCommand(request.AccessToken, refreshToken));

            SetRefreshTokenCookie(newRefreshToken);

            return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Token refreshed successfully"));
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

    [HttpPost("verify-email")]
    public async Task<IActionResult> EmailVerify([FromBody] VerifyEmailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Token is required"));
            }

            await Mediator.Send(new VerifyEmailCommand(request));

            return Ok(ApiResponse<object>.Ok(null, "Email verified successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPost("resend-email")]
    public async Task<IActionResult> ResendEmailConfirmation([FromBody] EmailResendConfirmationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
            }

            await Mediator.Send(new ResendEmailConfirmationCommand(request));

            return Ok(ApiResponse<object>.Ok(null, "Email sent successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> SendPasswordEmailChanging([FromBody] EmailResendConfirmationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
            }

            await Mediator.Send(new SendEmailPasswordChangingCommand(request));

            return Ok(ApiResponse<object>.Ok(null, "Email sent successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPost("password-reset")]
    public async Task<IActionResult> PasswordChangingRequest([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(ApiResponse<object>.Error(400, "NewPassword is required"));
            }

            await Mediator.Send(new ResetPasswordCommand(request));

            return Ok(ApiResponse<object>.Ok(null, "Password reset successfully"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpPost("validate-reset-token")]
    public async Task<IActionResult> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(ApiResponse<object>.Error(400, "TOKEN_INVALID"));
            }

            var isExpired = await Mediator.Send(new ValidateResetTokenCommand(request));

            if (isExpired)
            {
                return BadRequest(ApiResponse<object>.Error(400, "TOKEN_EXPIRED"));
            }

            return Ok(ApiResponse<object>.Ok(null, "valid"));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<object>.Error(500, ex.Message));
        }
    }

    [HttpGet("test-auth")]
    [Authorize]
    public IActionResult TestAuth()
    {
        var data = new { message = "You are authorized!", user = User.Identity?.Name };
        return Ok(ApiResponse<object>.Ok(data, "Authorization verified"));
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddMinutes(3)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
