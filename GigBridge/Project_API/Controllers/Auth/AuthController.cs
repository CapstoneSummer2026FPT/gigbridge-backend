using Application.Common.Models;
using Application.Features.Auth.ForgotPassword.Commands;
using Application.Features.Auth.ForgotPassword.DTOs;
using Application.Features.Auth.GoogleLogin.Commands;
using Application.Features.Auth.GoogleLogin.DTOs;
using Application.Features.Auth.Login.Commands;
using Application.Features.Auth.Login.DTOs;
using Application.Features.Auth.Logout.Commands;
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
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Project_API.Controllers.Common;
using Project_API.Security;
using System;
using System.Threading.Tasks;

namespace Project_API.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    private const int MaximumRefreshTokenCookieCandidates = 8;

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
        var refreshTokens = GetRefreshTokenCandidates();
        if (refreshTokens.Count == 0)
            return Unauthorized(ApiResponse<object>.Error(401, "Refresh token is missing. Please log in again."));

        var result = await Mediator.Send(new RefreshTokenCommand(
            request.AccessToken,
            refreshTokens[0],
            refreshTokens));

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiry);
        return Ok(ApiResponse<LoginResponse>.Ok(result.LoginData, "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    public async Task<IActionResult> Logout()
    {
        if (!await IsAllowedBrowserOriginAsync())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Error(403, "Logout origin is not allowed."));
        }

        try
        {
            await Mediator.Send(new LogoutCommand(GetRefreshTokenCandidates()));
            return Ok(ApiResponse<object?>.Ok(null, "Logout successful"));
        }
        finally
        {
            // Clearing the browser session must not depend on database availability.
            DeleteAllRefreshTokenCookies();
        }
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
        DeleteRefreshTokenCookie("/");
        DeleteRefreshTokenCookie("/api");

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

    private IReadOnlyList<string> GetRefreshTokenCandidates()
    {
        var refreshTokens = new List<string>();

        // A production release changed the cookie path from "/" to "/api/auth".
        // Browsers can therefore send both cookies with the same name until the old one
        // expires. Preserve every value so Application can validate the current token
        // instead of depending on duplicate-cookie parser ordering.
        foreach (var header in Request.Headers.Cookie)
        {
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            foreach (var segment in header.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = segment.IndexOf('=');
                if (separatorIndex <= 0 ||
                    !segment[..separatorIndex].Trim().Equals("refreshToken", StringComparison.Ordinal))
                {
                    continue;
                }

                var encodedValue = segment[(separatorIndex + 1)..].Trim().Trim('"');
                if (encodedValue.Length == 0)
                {
                    continue;
                }

                try
                {
                    AddRefreshTokenCandidate(refreshTokens, Uri.UnescapeDataString(encodedValue));
                }
                catch (UriFormatException)
                {
                    // Ignore malformed legacy cookie values. The canonical parsed cookie
                    // below remains available when ASP.NET Core can decode it safely.
                }
            }
        }

        if (Request.Cookies.TryGetValue("refreshToken", out var parsedRefreshToken))
        {
            AddRefreshTokenCandidate(refreshTokens, parsedRefreshToken);
        }

        return refreshTokens;
    }

    private static void AddRefreshTokenCandidate(List<string> candidates, string? refreshToken)
    {
        if (candidates.Count < MaximumRefreshTokenCookieCandidates &&
            !string.IsNullOrWhiteSpace(refreshToken) &&
            !candidates.Contains(refreshToken, StringComparer.Ordinal))
        {
            candidates.Add(refreshToken);
        }
    }

    private void DeleteAllRefreshTokenCookies()
    {
        DeleteRefreshTokenCookie("/api/auth");
        DeleteRefreshTokenCookie("/api");
        DeleteRefreshTokenCookie("/");
    }

    private async Task<bool> IsAllowedBrowserOriginAsync()
    {
        var origin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        var corsPolicyProvider = HttpContext.RequestServices
            .GetRequiredService<ICorsPolicyProvider>();
        var policy = await corsPolicyProvider.GetPolicyAsync(
            HttpContext,
            Project_API.Extensions.ServiceCollectionExtensions.FrontendCorsPolicy);

        return policy?.IsOriginAllowed(origin) == true;
    }

    private void DeleteRefreshTokenCookie(string path)
    {
        // Append each tombstone explicitly. Response.Cookies.Delete can replace an
        // earlier Set-Cookie header with the same name, which would leave cookies at
        // the other legacy paths alive.
        Response.Cookies.Append("refreshToken", string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = path,
            Expires = DateTimeOffset.UnixEpoch,
            MaxAge = TimeSpan.Zero
        });
    }

}
