using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Features.Chat.Common.Schedules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_API.Controllers.Common;

namespace Project_API.Controllers.Integrations;

[ApiController]
[Route("api/integrations/google-meet")]
public class GoogleMeetController : BaseApiController
{
    private readonly IGoogleMeetOAuthService _meetOAuth;

    public GoogleMeetController(IGoogleMeetOAuthService meetOAuth)
    {
        _meetOAuth = meetOAuth;
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

    [HttpGet("callback")]
    [AllowAnonymous]
    public IActionResult Callback(
        [FromQuery] string? state,
        [FromQuery] string? code,
        [FromQuery] string? error)
    {
        // The callback is authenticated via the one-time state record.
        // We need the userId from the state. Since the callback is a redirect from Google,
        // we need to find the state record first and derive the user.
        if (string.IsNullOrEmpty(state))
            return Redirect(GetFrontendCallbackUrl("missing_state", null));

        var stateHash = ConvertToSha256Hex(state);
        // We can't directly access the state from here without a service method.
        // For simplicity, redirect to frontend with the raw params and let the frontend
        // pass them through the API.
        return Redirect(GetFrontendCallbackUrl("processing", state, code, error));
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

    private string GetFrontendCallbackUrl(
        string result,
        string? state = null,
        string? code = null,
        string? error = null)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return $"{baseUrl}/integrations/google-meet/callback?result={Uri.EscapeDataString(result)}" +
               (string.IsNullOrEmpty(state) ? "" : $"&state={Uri.EscapeDataString(state)}") +
               (string.IsNullOrEmpty(code) ? "" : $"&code={Uri.EscapeDataString(code)}") +
               (string.IsNullOrEmpty(error) ? "" : $"&error={Uri.EscapeDataString(error)}");
    }

    private static string ConvertToSha256Hex(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

public record GoogleMeetCallbackRequest(string State, string? Code, string? Error);
