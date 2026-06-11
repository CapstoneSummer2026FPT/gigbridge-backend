using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Proposal
{
    public Guid ProposalsId { get; set; }

    public Guid JobPostsId { get; set; }

    public Guid FreelancerProfilesId { get; set; }

    public string? CoverLetter { get; set; }

    public decimal? ProposedBudget { get; set; }

    public string? ProposedDuration { get; set; }

    /// <summary>
    /// Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn, 5=Draft
    /// </summary>
    public int Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAigenerated { get; set; }

    public virtual Contract? Contract { get; set; }

    public virtual FreelancerProfile FreelancerProfiles { get; set; } = null!;

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual ICollection<ProposalAttachment> ProposalAttachments { get; set; } = new List<ProposalAttachment>();

    public virtual ICollection<ProposalAnswer> ProposalAnswers { get; set; } = new List<ProposalAnswer>();
}
