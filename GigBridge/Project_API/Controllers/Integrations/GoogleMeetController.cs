using Application.Common.InternalServices.Chat.Interfaces;
using Application.Common.Models;
using Application.Features.Chat.Common.Messages.CreateGoogleMeet;
using Application.Features.Chat.Common.Messages.Send.DTOs;
using Application.Features.Chat.Common.Schedules;
using Infrastructure.ExternalServices.Google.Meet;
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

    public GoogleMeetController(IGoogleMeetOAuthService meetOAuth, IOptions<GoogleMeetOptions> options)
    {
        _meetOAuth = meetOAuth;
        _options = options.Value;

        if (!Uri.TryCreate(_options.FrontendCallbackUri, UriKind.Absolute, out var callbackUri)
            || (callbackUri.Scheme != Uri.UriSchemeHttp && callbackUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "GoogleMeet:FrontendCallbackUri must be an absolute HTTP(S) URL.");
        }
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

    [HttpPost("rooms")]
    [Authorize]
    public async Task<IActionResult> CreateRoomAndSendMessage(
        [FromBody] CreateGoogleMeetMessageRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return InvalidTokenResponse();

        var message = await Mediator.Send(
            new CreateGoogleMeetMessageCommand(userId, request),
            HttpContext.RequestAborted);

        return Ok(ApiResponse<MessageResponse>.Ok(message, "Google Meet room created and sent"));
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
        var baseUrl = _options.FrontendCallbackUri;

        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}result={Uri.EscapeDataString(result)}" +
               (string.IsNullOrEmpty(state) ? "" : $"&state={Uri.EscapeDataString(state)}") +
               (string.IsNullOrEmpty(code) ? "" : $"&code={Uri.EscapeDataString(code)}") +
               (string.IsNullOrEmpty(error) ? "" : $"&error={Uri.EscapeDataString(error)}");
    }
}

public record GoogleMeetCallbackRequest(string State, string? Code, string? Error);
