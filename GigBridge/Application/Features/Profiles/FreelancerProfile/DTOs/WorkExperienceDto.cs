using System;

namespace Application.Features.Profiles.FreelancerProfile.DTOs;

public class WorkExperienceDto
{
    public Guid WorkExperienceId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string JobTitle { get; set; } = null!;
    public string? Description { get; set; }
    public string StartDate { get; set; } = null!;
    public string? EndDate { get; set; }
}
