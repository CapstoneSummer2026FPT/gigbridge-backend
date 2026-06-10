using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class JobPost
{
    public Guid JobPostsId { get; set; }

    public Guid ClientProfilesId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public Guid? CategoryId { get; set; }

    /// <summary>
    //BudgetType: Fixed
    /// </summary>
    public int BudgetType { get; set; }

    public decimal? BudgetMin { get; set; }

    public decimal? BudgetMax { get; set; }

    public string? Currency { get; set; }

    public string? EstimatedDuration { get; set; }

    public int? MaxHires { get; set; }

    /// <summary>
    /// Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert
    /// </summary>
    public int? ExperienceLevelRequired { get; set; }

    public string? Location { get; set; }

    /// <summary>
    /// Enum JobPostStatus: 0=Draft (Client), 1=Open (All), 2=Closed(Client), 3=Cancelled(Client,Admin)
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly
    /// </summary>
    public int? Visibility { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsAigenerated { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ClientProfile ClientProfiles { get; set; } = null!;

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<EsignDocument> EsignDocuments { get; set; } = new List<EsignDocument>();

    public virtual ICollection<JobPostAttachment> JobPostAttachments { get; set; } = new List<JobPostAttachment>();

    public virtual ICollection<JobPostSkill> JobPostSkills { get; set; } = new List<JobPostSkill>();

    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();

    public virtual ICollection<SavedJob> SavedJobs { get; set; } = new List<SavedJob>();
}
