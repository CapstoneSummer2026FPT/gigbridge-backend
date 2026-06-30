using System;

namespace Domain.Entities;

public partial class ProposalInterviewReviewSession
{
    public Guid ProposalInterviewReviewSessionsId { get; set; }

    public Guid ProposalsId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsLocked { get; set; }

    public int ReviewableQuestionCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Proposal Proposals { get; set; } = null!;

    public virtual User FreelancerUser { get; set; } = null!;
}
