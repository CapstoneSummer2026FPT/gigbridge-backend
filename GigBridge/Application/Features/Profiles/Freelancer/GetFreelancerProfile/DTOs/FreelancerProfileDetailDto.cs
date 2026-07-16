using System;
using System.Collections.Generic;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;

public class FreelancerProfileDetailDto
{
    public Guid FreelancerProfilesId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public int? Availability { get; set; }
    public string? Location { get; set; }
    public int? ProfileCompletionScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public List<FreelancerProfileCategoryDto> Categories { get; set; } = new();

    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAvatar { get; set; }
    public double Rating { get; set; }
    public int EloPoints { get; set; }
    public bool IsPremium { get; set; }
    public bool IsIdentityVerified { get; set; }
    public bool ShowProVerifiedBadge { get; set; }
    public DateTime? PremiumUntil { get; set; }
    public string? TierName { get; set; }
    public decimal TierProgress { get; set; }

    public List<FreelancerSkillDto> Skills { get; set; } = new();
    public List<PortfolioItemDto> PortfolioItems { get; set; } = new();
    public List<WorkExperienceDto> WorkExperiences { get; set; } = new();
    public int CheatingViolationCount { get; set; }
    public List<CheatingPenaltyLogDto> CheatingPenaltyLogs { get; set; } = new();
}

public class CheatingPenaltyLogDto
{
    public Guid ViolationId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid JobPostId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public int ViolationNumber { get; set; }
    public int TotalEventCount { get; set; }
    public int CopyCount { get; set; }
    public int PasteCount { get; set; }
    public int TabSwitchCount { get; set; }
    public int ScreenshotAttemptCount { get; set; }
    public int FocusLossCount { get; set; }
    public int FullscreenExitCount { get; set; }
    public int Action { get; set; }
    public int EloDelta { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
