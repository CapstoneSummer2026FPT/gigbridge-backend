using Domain.Enums;

namespace Domain.Entities;

public class GoogleMeetConnection
{
    public Guid GoogleMeetConnectionId { get; set; }
    public Guid UserId { get; set; }
    public string GoogleSubject { get; set; } = string.Empty;
    public string GoogleEmail { get; set; } = string.Empty;
    public string GrantedScopes { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public GoogleMeetConnectionStatus Status { get; set; }
    public string? LastFailureCode { get; set; }
    public int Version { get; set; } = 1;
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastRefreshedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
