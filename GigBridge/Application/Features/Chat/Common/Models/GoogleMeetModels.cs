namespace Application.Features.Chat.Common.Models;

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
