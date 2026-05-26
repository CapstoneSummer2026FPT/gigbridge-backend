using Application.DTOs.Admin;

namespace Application.Features.Admin.Users.Dto;

public sealed class UserPageQueryDto : PagedQueryDto
{
    public bool? IsActive { get; set; }
    public int? Role { get; set; }
}

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
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminUserDetailDto : AdminUserDto
{
    public AdminClientProfileDto? ClientProfile { get; init; }
    public AdminFreelancerProfileDto? FreelancerProfile { get; init; }
}

public sealed class AdminClientProfileDto
{
    public Guid ClientProfileId { get; init; }
    public string? CompanyName { get; init; }
    public string? Industry { get; init; }
    public string? Location { get; init; }
}

public sealed class AdminFreelancerProfileDto
{
    public Guid FreelancerProfileId { get; init; }
    public string? Title { get; init; }
    public decimal? HourlyRate { get; init; }
    public int? ExperienceLevel { get; init; }
    public int? Availability { get; init; }
    public string? Location { get; init; }
}

public sealed class UserStatusRequestDto
{
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public sealed class SaveAdminUserRequestDto
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public int Role { get; init; }
    public bool IsEmailVerified { get; init; }
    public string? PreferredLanguage { get; init; }
}
