using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Skill
{
    public Guid SkillsId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CategorySkill> CategorySkills { get; set; } = new List<CategorySkill>();

    public virtual ICollection<FreelancerSkill> FreelancerSkills { get; set; } = new List<FreelancerSkill>();

    public virtual ICollection<JobPostSkill> JobPostSkills { get; set; } = new List<JobPostSkill>();
}
