namespace Domain.Entities;

public sealed class AuthSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string RefreshTokenHash { get; set; } = null!;

    public DateTime RefreshTokenExpiry { get; set; }

    public string? PreviousRefreshTokenHash { get; set; }

    public DateTime? PreviousRefreshTokenGraceExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUsedAt { get; set; }

    public User User { get; set; } = null!;
}
