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
using Application.Features.Auth.ResetPassword.Commands;
using Application.Features.Auth.ResetPassword.DTOs;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Auth.SendOtp.Commands;
using Application.Features.Auth.SendOtp.DTOs;
using Application.Features.Auth.VerifyOtp.Commands;
using Application.Features.Auth.VerifyOtp.DTOs;
using Application.Features.Auth.ChangePassword.Commands;
using Application.Features.Auth.ChangePassword.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_API.Controllers.Common;
using Project_API.Security;
using System;
using System.Threading.Tasks;

namespace Project_API.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.Account)]
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
    [EnableRateLimiting(AuthRateLimitPolicies.OtpIssue)]
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
    [EnableRateLimiting(AuthRateLimitPolicies.OtpVerify)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(ApiResponse<object>.Error(400, "Email and verification code are required"));
        }

        var verification = await Mediator.Send(new VerifyOtpCommand(request));
        return Ok(ApiResponse<VerifyOtpResponse>.Ok(
            verification,
            "Email verified successfully"));
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
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
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
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
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    public async Task<IActionResult> Refresh([FromBody] TokenRequest request)
    {
         var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse<object>.Error(401, "Refresh token is missing. Please log in again."));

        var result = await Mediator.Send(new RefreshTokenCommand(request.AccessToken, refreshToken));

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiry);
        return Ok(ApiResponse<LoginResponse>.Ok(result.LoginData, "Token refreshed successfully"));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordProfileRequest? request)
    {

        if (request is null || string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.BadRequest("Current password and new password are required"));

        await Mediator.Send(new ChangePasswordCommand(request));
        return Ok(ApiResponse<object>.NoContent("Password changed successfully"));
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.OtpIssue)]
    public async Task<IActionResult> SendPasswordEmailChanging([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        await Mediator.Send(new SendEmailPasswordChangingCommand(request));
        return Ok(ApiResponse<object>.NoContent("Email sent successfully"));
    }

    [HttpPost("password-reset")]
    [EnableRateLimiting(AuthRateLimitPolicies.OtpVerify)]
    public async Task<IActionResult> PasswordChangingRequest([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(ApiResponse<object>.BadRequest("Email is required"));

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(ApiResponse<object>.BadRequest("NewPassword is required"));

        await Mediator.Send(new ResetPasswordCommand(request));
        return Ok(ApiResponse<object>.NoContent("Password reset successfully"));
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/api/auth",
            Expires = expires
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

}
