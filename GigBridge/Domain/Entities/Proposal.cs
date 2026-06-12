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
    /// Enum ProposalStatus: 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn
    /// </summary>
    public int Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAigenerated { get; set; }

    public virtual Contract? Contract { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual FreelancerProfile FreelancerProfiles { get; set; } = null!;

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual ICollection<NegotiationOffer> NegotiationOffers { get; set; } = new List<NegotiationOffer>();

    public virtual ICollection<ProposalAttachment> ProposalAttachments { get; set; } = new List<ProposalAttachment>();

    public virtual ICollection<ProposalAnswer> ProposalAnswers { get; set; } = new List<ProposalAnswer>();
}
