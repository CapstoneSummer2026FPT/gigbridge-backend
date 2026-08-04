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

    public string? AnalysisSummary { get; set; }

    public string? SolutionApproach { get; set; }

    public string? Deliverables { get; set; }

    public string? Assumptions { get; set; }

    public string? OutOfScope { get; set; }

    /// <summary>
    /// Enum ProposalStatus: 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn
    /// </summary>
    public int Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAigenerated { get; set; }

    /// <summary>Independent Admin moderation state. Proposal lifecycle in Status is never overwritten by moderation.</summary>
    public int ModerationStatus { get; set; }

    public Guid? InvalidatedByAdminId { get; set; }

    public DateTime? InvalidatedAt { get; set; }

    public string? InvalidationReason { get; set; }

    public virtual User? InvalidatedByAdmin { get; set; }

    public virtual ICollection<ProposalAdminNote> AdminNotes { get; set; } = new List<ProposalAdminNote>();

    public virtual Contract? Contract { get; set; }

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual FreelancerProfile FreelancerProfiles { get; set; } = null!;

    public virtual JobPost JobPosts { get; set; } = null!;

    public virtual ICollection<NegotiationOffer> NegotiationOffers { get; set; } = new List<NegotiationOffer>();

    public virtual ICollection<ProposalAnswer> ProposalAnswers { get; set; } = new List<ProposalAnswer>();

    public virtual ICollection<ProposalQuestionTimer> ProposalQuestionTimers { get; set; } = new List<ProposalQuestionTimer>();

    public virtual ICollection<ProposalWorkBreakdownItem> ProposalWorkBreakdownItems { get; set; } = new List<ProposalWorkBreakdownItem>();

    public virtual ICollection<ProposalMilestonePlan> ProposalMilestonePlans { get; set; } = new List<ProposalMilestonePlan>();

    public virtual ProposalInterviewReviewSession? ProposalInterviewReviewSession { get; set; }

    public virtual ProposalAiJudging? ProposalAiJudging { get; set; }
}
