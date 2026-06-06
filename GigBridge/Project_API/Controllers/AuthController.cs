using Application.Common.Models;
using Application.Features.Auth.ForgotPassword.Commands;
using Application.Features.Auth.ForgotPassword.DTOs;
using Application.Features.Auth.GoogleLogin.Commands;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Login.Commands;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.RefreshToken.Commands;
using Application.Features.Auth.RefreshToken.DTOs;
using Application.Features.Auth.Register.Commands;
using Application.Features.Auth.Register.DTOs;
using Application.Features.Auth.ResendEmail.Commands;
using Application.Features.Auth.ResendEmail.DTOs;
using Application.Features.Auth.ResetPassword.Commands;
using Application.Features.Auth.ResetPassword.DTOs;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.ValidateToken.Commands;
using Application.Features.Auth.ValidateToken.DTOs;
using Application.Features.Auth.VerifyEmail.Commands;
using Application.Features.Auth.VerifyEmail.DTOs;
using Application.Features.Auth.SendOtp.Commands;
using Application.Features.Auth.SendOtp.DTOs;
using Application.Features.Auth.VerifyOtp.Commands;
using Application.Features.Auth.VerifyOtp.DTOs;
using Application.Features.Auth.ChangePassword.Commands;
using Application.Features.Auth.ChangePassword.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Project_API.Controllers.Common;
using System;
using System.Threading.Tasks;

namespace Project_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
            return BadRequest(ApiResponse<object>.BadRequest("Registration data is required"));

        var user = await Mediator.Send(new RegisterCommand(request));

        if (user == null)
            return BadRequest(ApiResponse<object>.BadRequest("Registration failed"));

        return Ok(ApiResponse<UserDTO>.Ok(user, "User registered successfully"));
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Email is required"));
        }

        await Mediator.Send(new SendOtpCommand(request));
        return Ok(ApiResponse<object?>.Ok(null, "Verification code sent successfully"));
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Email and verification code are required"));
        }

        await Mediator.Send(new VerifyOtpCommand(request));
        return Ok(ApiResponse<object?>.Ok(null, "Email verified successfully"));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null)
            return BadRequest(ApiResponse<object>.BadRequest("Login data is required"));

        var (loginData, refreshToken, refreshTokenExpiry) = await Mediator.Send(new LoginWithRefreshCommand(request));

        if (loginData == null)
            return BadRequest(ApiResponse<object>.BadRequest("Login failed"));

        SetRefreshTokenCookie(refreshToken, refreshTokenExpiry);
        return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Login successful"));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.AuthCode))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Authorization code is required"));
        }

        var (loginData, refreshToken, refreshTokenExpiry) = await Mediator.Send(new GoogleLoginCommand(request.AuthCode, request.Role, request.IsFromSignIn));

        if (loginData == null)
            return BadRequest(ApiResponse<object>.BadRequest("Google login failed"));

        SetRefreshTokenCookie(refreshToken, refreshTokenExpiry);
        return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Login successful"));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest request)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse<object>.Error(401, "Refresh token is missing. Please log in again."));

        var (loginData, newRefreshToken, newRefreshTokenExpiry) = await Mediator.Send(new RefreshTokenCommand(request.AccessToken, refreshToken));

        SetRefreshTokenCookie(newRefreshToken, newRefreshTokenExpiry);
        return Ok(ApiResponse<LoginResponse>.Ok(loginData, "Token refreshed successfully"));
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> EmailVerify([FromQuery] VerifyEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponse<object>.BadRequest("Token is required"));

        await Mediator.Send(new VerifyEmailCommand(request));
        return Ok(ApiResponse<object>.NoContent("Email verified successfully"));
    }

    [HttpPost("resend-email")]
    public async Task<IActionResult> ResendEmailConfirmation([FromBody] EmailResendConfirmationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        await Mediator.Send(new ResendEmailConfirmationCommand(request));
        return Ok(ApiResponse<object>.NoContent("Email sent successfully"));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordProfileRequest request)
    {
        if (request == null)
            return BadRequest(ApiResponse<object>.BadRequest("Request body cannot be null"));

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.BadRequest("Current password and new password are required"));

        await Mediator.Send(new ChangePasswordCommand(request));
        return Ok(ApiResponse<object>.NoContent("Password changed successfully"));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> SendPasswordEmailChanging([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        await Mediator.Send(new SendEmailPasswordChangingCommand(request));
        return Ok(ApiResponse<object>.NoContent("Email sent successfully"));
    }

    [HttpPost("password-reset")]
    public async Task<IActionResult> PasswordChangingRequest([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.BadRequest("NewPassword is required"));

        await Mediator.Send(new ResetPasswordCommand(request));
        return Ok(ApiResponse<object>.NoContent("Password reset successfully"));
    }

    [HttpPost("validate-reset-token")]
    public async Task<IActionResult> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(ApiResponse<object>.BadRequest("TOKEN_INVALID"));

        var isExpired = await Mediator.Send(new ValidateResetTokenCommand(request));

        if (isExpired)
            return BadRequest(ApiResponse<object>.BadRequest("TOKEN_EXPIRED"));

        return Ok(ApiResponse<object>.NoContent("valid"));
    }

    [HttpGet("test-auth")]
    [Authorize]
    public IActionResult TestAuth()
    {
        var data = new { message = "You are authorized!", user = User.Identity?.Name };
        return Ok(ApiResponse<object>.Ok(data, "Authorization verified"));
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expires
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}
