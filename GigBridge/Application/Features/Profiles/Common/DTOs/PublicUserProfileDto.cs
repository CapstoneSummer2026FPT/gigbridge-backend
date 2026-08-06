namespace Application.Features.Profiles.Common.DTOs;

public sealed class PublicUserProfileDto
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = null!;
    public string? Avatar { get; init; }
    public int Role { get; init; }
    public bool IsPremium { get; init; }
}
