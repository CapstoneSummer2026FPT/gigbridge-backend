namespace Domain.Entities;

/// <summary>
/// One attempt by the freelancer to deliver a work item, plus the client's verdict on it.
///
/// Rows are append-only: a revision request never edits an attempt, it creates the next one. That is what
/// keeps the earlier files, note and rejection reason visible after a resubmission, which is the evidence
/// an admin needs when the contract ends up in a dispute.
/// </summary>
public sealed class ContractWorkItemSubmission
{
    public Guid ContractWorkItemSubmissionId { get; set; }

    public Guid ContractWorkItemId { get; set; }

    /// <summary>1-based, unique per work item.</summary>
    public int RevisionNumber { get; set; }

    /// <summary>
    /// Client-generated, one per "submit selected" action. Unique per work item, so a retried HTTP request
    /// carrying the same batch id cannot create a second attempt.
    /// </summary>
    public Guid SubmissionBatchId { get; set; }

    public string? Note { get; set; }

    public DateTime SubmittedAt { get; set; }

    public Guid SubmittedByUserId { get; set; }

    /// <summary>
    /// Enum ContractWorkItemSubmissionReviewStatus: 0=Submitted, 1=Approved, 2=RevisionRequired
    /// </summary>
    public int ReviewStatus { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewReason { get; set; }

    public ContractWorkItem ContractWorkItem { get; set; } = null!;

    public User? SubmittedByUser { get; set; }

    public User? ReviewedByUser { get; set; }

    public ICollection<MilestoneAttachment> Attachments { get; set; } = new List<MilestoneAttachment>();
}
