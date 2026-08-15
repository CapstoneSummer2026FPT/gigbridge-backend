namespace Application.Features.Seo.PublicMarketplace.DTOs;

public sealed record PublicFreelancerSkillDto(string SkillName);

/// <summary>
/// Minimal public-safe marketplace directory entry for an active freelancer.
/// </summary>
public sealed record PublicFreelancerSummaryDto(
    Guid UserId,
    string? UserFullName,
    string? UserAvatar,
    string? Title,
    string? Bio,
    string? Location,
    string? MajorName,
    double Rating,
    DateTime? UpdatedAt,
    List<PublicFreelancerSkillDto> Skills);
