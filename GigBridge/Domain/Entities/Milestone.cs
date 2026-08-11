using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Milestone
{
    public Guid MilestonesId { get; set; }

    public Guid ContractsId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? EstimatedDuration { get; set; }

    public string? Deliverables { get; set; }

    public string? AcceptanceCriteria { get; set; }

    /// <summary>
    /// Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed, 7=Cancelled, 8=Completed
    /// </summary>
    public int Status { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public decimal ReleasedAmount { get; set; }

    /// <summary>
    /// Running total of money permanently removed from this milestone's escrow via dispute
    /// resolution or admin override (client refund + platform penalty). Distinct from
    /// ReleasedAmount (paid to the freelancer); Amount - ReleasedAmount - RefundedAmount is
    /// what genuinely remains payable for this milestone.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    public DateTime? LastReleasedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? SubmissionDescription { get; set; }

    public virtual Contract Contracts { get; set; } = null!;

    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();

    public virtual ICollection<ReportContract> ReportContracts { get; set; } = new List<ReportContract>();

    public virtual ICollection<EscrowTransaction> EscrowTransactions { get; set; } = new List<EscrowTransaction>();

    public virtual ICollection<MilestoneAttachment> MilestoneAttachments { get; set; } = new List<MilestoneAttachment>();

    public virtual ICollection<ContractWorkItem> WorkItems { get; set; } = new List<ContractWorkItem>();
}
