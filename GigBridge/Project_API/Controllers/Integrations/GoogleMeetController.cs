using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Features.Chat.Common.Schedules;
using Infrastructure.ExternalServices.GoogleMeet;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Integrations;

[ApiController]
[Route("api/integrations/google-meet")]
public class GoogleMeetController : BaseApiController
{
    private readonly IGoogleMeetOAuthService _meetOAuth;
    private readonly GoogleMeetOptions _options;

    public GoogleMeetController(
        IGoogleMeetOAuthService meetOAuth,
        IOptions<GoogleMeetOptions> options)
    {
        _meetOAuth = meetOAuth;
        _options = options.Value;
    }

    [HttpPost("authorization-url")]
    [Authorize]
    public async Task<IActionResult> GetAuthorizationUrl()
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await _meetOAuth.GetAuthorizationUrlAsync(userId, HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(new
        {
            authorizationUrl = result.Url,
            expiresAt = result.ExpiresAt,
            flowId = result.FlowId
        }, "Authorization URL generated"));
    }

    /// <summary>
    /// Google redirects here after user authorization. Redirects to the frontend
    /// callback route which will complete the flow via POST /api/integrations/google-meet/callback.
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error)
    {
        var frontendUri = string.IsNullOrWhiteSpace(_options.FrontendCallbackUri)
            ? $"{Request.Scheme}://{Request.Host}/integrations/google-meet/callback"
            : _options.FrontendCallbackUri;

        return Redirect($"{frontendUri}?state={Uri.EscapeDataString(state ?? "")}" +
                        $"&code={Uri.EscapeDataString(code ?? "")}" +
                        $"&error={Uri.EscapeDataString(error ?? "")}");
    }

    [HttpPost("callback")]
    [Authorize]
    public async Task<IActionResult> CompleteCallback(
        [FromBody] GoogleMeetCallbackRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var result = await _meetOAuth.HandleCallbackAsync(
            userId, request.State, request.Code, request.Error,
            HttpContext.RequestAborted);

        return Ok(ApiResponse<object>.Ok(new { result }, "Callback processed"));
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var status = await _meetOAuth.GetStatusAsync(userId, HttpContext.RequestAborted);
        return Ok(ApiResponse<object>.Ok(new
        {
            isConnected = status.IsConnected,
            googleEmail = status.GoogleEmail,
            connectedAt = status.ConnectedAt,
            needsReconnect = status.NeedsReconnect
        }, "Success"));
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> Disconnect()
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        await _meetOAuth.DisconnectAsync(userId, HttpContext.RequestAborted);
        return NoContent();
    }
}

public record GoogleMeetCallbackRequest(string State, string? Code, string? Error);
