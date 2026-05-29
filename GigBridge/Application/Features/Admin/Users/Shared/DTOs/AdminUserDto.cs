namespace Application.Features.Admin.Users.Shared.DTOs;

public class AdminUserDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string? PhoneNumber { get; init; }
    public int Role { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool IsActive { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? Provider { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
