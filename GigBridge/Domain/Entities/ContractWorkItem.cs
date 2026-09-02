namespace Domain.Entities;

public sealed class ContractWorkItem
{
    public Guid ContractWorkItemId { get; set; }
    public Guid MilestonesId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// Legacy free text. No longer authored anywhere in the UI; retained because signed contracts
    /// rendered before the work-item delivery flow still reference it.
    /// </summary>
    public string? Deliverables { get; set; }

    public string? EstimatedDuration { get; set; }

    /// <summary>
    /// Server-computed from the parent milestone's start and the ordered work item durations.
    /// Any client-supplied value is overwritten on save. Null means "not scheduled" and never
    /// participates in a workflow rule.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// Enum ContractWorkItemStatus: 0=Todo, 1=InProgress, 2=Completed (legacy), 3=RevisionRequired,
    /// 4=Submitted, 5=Approved
    /// </summary>
    public int Status { get; set; }

    public string? ProgressNote { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Milestone Milestone { get; set; } = null!;

    /// <summary>
    /// Append-only submission history, ordered by <see cref="ContractWorkItemSubmission.RevisionNumber"/>.
    /// <see cref="Status"/> is the denormalized aggregate of the latest attempt and is written in the
    /// same transaction.
    /// </summary>
    public ICollection<ContractWorkItemSubmission> Submissions { get; set; } = new List<ContractWorkItemSubmission>();
}
