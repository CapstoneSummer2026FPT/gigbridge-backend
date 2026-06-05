using System;

namespace Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;

public class FreelancerSkillDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = null!;
    public int? ProficiencyLevel { get; set; }
}
