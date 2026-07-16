using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class FreelancerProfile
{
    public Guid FreelancerProfilesId { get; set; }

    public Guid UserId { get; set; }

    public Guid? MajorId { get; set; }

    public string? Title { get; set; }

    public string? Bio { get; set; }

    /// <summary>
    /// Enum Availability: 0=FullTime, 1=PartTime, 2=NotAvailable
    /// </summary>
    public int? Availability { get; set; }

    public string? Location { get; set; }

    public int? ProfileCompletionScore { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<FreelancerSkill> FreelancerSkills { get; set; } = new List<FreelancerSkill>();

    public virtual ICollection<FreelancerProfileCategory> FreelancerProfileCategories { get; set; } = new List<FreelancerProfileCategory>();

    public virtual Major? Major { get; set; }
    public virtual ICollection<FreelancerRankProtection> RankProtections { get; set; } = new List<FreelancerRankProtection>();
    public virtual ICollection<FreelancerProfilePromotion> Promotions { get; set; } = new List<FreelancerProfilePromotion>();

    public virtual ICollection<PortfolioItem> PortfolioItems { get; set; } = new List<PortfolioItem>();

    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();

    public virtual ICollection<SavedFreelancer> SavedFreelancers { get; set; } = new List<SavedFreelancer>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WorkExperience> WorkExperiences { get; set; } = new List<WorkExperience>();
}
