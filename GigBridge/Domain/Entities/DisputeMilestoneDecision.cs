namespace Domain.Entities;

public sealed class DisputeMilestoneDecision
{
    public Guid DisputeMilestoneDecisionId { get; set; }
    public Guid DisputesId { get; set; }
    public Guid MilestonesId { get; set; }
    public int Outcome { get; set; }
    public decimal MilestoneAmountSnapshot { get; set; }
    public decimal ReleasedAmountSnapshot { get; set; }
    public decimal AdditionalReleaseAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public Guid DecidedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Dispute Dispute { get; set; } = null!;
    public Milestone Milestone { get; set; } = null!;
    public User DecidedByAdmin { get; set; } = null!;
}
