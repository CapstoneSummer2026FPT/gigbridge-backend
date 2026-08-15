using Application.Common.InternalServices.Chat.Models;

namespace Application.Common.InternalServices.Chat.Interfaces;
public interface IGoogleMeetOAuthService
{
    Task<AuthorizationUrlResult> GetAuthorizationUrlAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<string> HandleCallbackAsync(
        Guid userId,
        string state,
        string? code,
        string? error,
        CancellationToken cancellationToken);

    Task<GoogleMeetConnectionStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<string?> GetAccessTokenAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
