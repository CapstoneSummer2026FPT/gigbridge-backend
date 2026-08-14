namespace Application.Features.Seo.PublicMarketplace.DTOs;

public sealed record PublicFreelancerSkillDto(string SkillName);

/// <summary>
/// Minimal directory entry for a freelancer who explicitly opted in to indexing.
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
