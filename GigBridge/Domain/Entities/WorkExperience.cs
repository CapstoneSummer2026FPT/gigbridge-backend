using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class WorkExperience
{
    public Guid WorkExperiencesId { get; set; }

    public Guid FreelancerId { get; set; }

    public string CompanyName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }

    public virtual FreelancerProfile Freelancer { get; set; } = null!;
}
