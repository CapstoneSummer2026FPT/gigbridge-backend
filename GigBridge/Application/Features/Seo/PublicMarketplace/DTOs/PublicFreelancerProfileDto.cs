using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;

namespace Application.Features.Seo.PublicMarketplace.DTOs;

/// <summary>
/// Search-engine-safe freelancer profile returned only after explicit opt-in.
/// </summary>
public sealed class PublicFreelancerProfileDto
{
    public Guid FreelancerProfilesId { get; init; }
    public Guid UserId { get; init; }
    public string? Title { get; init; }
    public string? Bio { get; init; }
    public int? Availability { get; init; }
    public string? Location { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? MajorId { get; init; }
    public string? MajorName { get; init; }
    public string? UserFullName { get; init; }
    public string? UserAvatar { get; init; }
    public double Rating { get; init; }
    public int EloPoints { get; init; }
    public bool IsPremium { get; init; }
    public bool IsIdentityVerified { get; init; }
    public bool ShowProVerifiedBadge { get; init; }
    public List<FreelancerProfileCategoryDto> Categories { get; init; } = [];
    public List<FreelancerSkillDto> Skills { get; init; } = [];
    public List<PortfolioItemDto> PortfolioItems { get; init; } = [];
    public List<WorkExperienceDto> WorkExperiences { get; init; } = [];
}
