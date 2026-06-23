namespace Application.Common.Interfaces.IService;

public record AuthorizationUrlResult(string Url, DateTime ExpiresAt, Guid FlowId);

public record GoogleMeetConnectionStatusResponse(
    bool IsConnected,
    string? GoogleEmail,
    DateTime? ConnectedAt,
    bool NeedsReconnect);

public record CreateMeetSpaceResult(
    bool IsSuccess,
    bool IsAmbiguous,
    string? SpaceName,
    string? MeetingUri,
    string? FailureCode);

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

public interface IGoogleMeetApiClient
{
    Task<CreateMeetSpaceResult> CreateSpaceAsync(
        string accessToken,
        CancellationToken cancellationToken);
}
