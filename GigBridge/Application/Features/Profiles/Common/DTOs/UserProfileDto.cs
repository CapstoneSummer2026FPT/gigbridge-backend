namespace Application.Features.Profiles.Common.DTOs;

public sealed class UserProfileDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string? Avatar { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PreferredLanguage { get; init; }
    public int Role { get; init; }
}
