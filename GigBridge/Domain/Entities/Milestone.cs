using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Milestone
{
    public Guid MilestonesId { get; set; }

    public Guid ContractsId { get; set; }

    public string Title { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed
    /// </summary>
    public int Status { get; set; }

    public int? SortOrder { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public decimal ReleasedAmount { get; set; }

    public DateTime? LastReleasedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Contract Contracts { get; set; } = null!;

    public virtual ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();

    public virtual ICollection<EscrowTransaction> EscrowTransactions { get; set; } = new List<EscrowTransaction>();

    public virtual ICollection<MilestoneAttachment> MilestoneAttachments { get; set; } = new List<MilestoneAttachment>();

    public virtual ICollection<PaymentProof> PaymentProofs { get; set; } = new List<PaymentProof>();
}
