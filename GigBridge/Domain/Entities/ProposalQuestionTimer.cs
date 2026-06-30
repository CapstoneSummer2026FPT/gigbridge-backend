using System;

namespace Domain.Entities;

public partial class ProposalQuestionTimer
{
    public Guid ProposalQuestionTimersId { get; set; }

    public Guid ProposalsId { get; set; }

    public Guid JobPostQuestionsId { get; set; }

    public Guid FreelancerUserId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsLocked { get; set; }

    public int? LockedReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Proposal Proposals { get; set; } = null!;

    public virtual JobPostQuestion JobPostQuestions { get; set; } = null!;

    public virtual User FreelancerUser { get; set; } = null!;
}
