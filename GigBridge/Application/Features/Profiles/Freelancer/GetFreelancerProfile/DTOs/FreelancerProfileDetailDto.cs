using System;
using System.Collections.Generic;

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

    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAvatar { get; set; }
    public double Rating { get; set; }
    public int EloPoints { get; set; }

    public List<FreelancerSkillDto> Skills { get; set; } = new();
    public List<PortfolioItemDto> PortfolioItems { get; set; } = new();
    public List<WorkExperienceDto> WorkExperiences { get; set; } = new();
}
