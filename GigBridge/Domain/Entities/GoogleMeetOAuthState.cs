namespace Domain.Entities;

public class GoogleMeetOAuthState
{
    public Guid GoogleMeetOAuthStateId { get; set; }
    public Guid UserId { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public string NonceHash { get; set; } = string.Empty;
    public string ProtectedCodeVerifier { get; set; } = string.Empty;
    public Guid FlowId { get; set; }
    public string FrontendReturnPath { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
