namespace Domain.Enums.Contracts;

public enum ContractWorkItemStatus
{
    Todo = 0,
    InProgress = 1,

    /// <summary>
    /// Legacy: the freelancer ticked the item done under the milestone-level delivery flow.
    /// Never written by the work-item delivery flow; kept because the value is persisted.
    /// </summary>
    Completed = 2,

    RevisionRequired = 3,

    /// <summary>
    /// The freelancer uploaded deliverables for this work item and is waiting for the client.
    /// </summary>
    Submitted = 4,

    /// <summary>
    /// The client accepted this work item. Terminal.
    /// </summary>
    Approved = 5
}

/// <summary>
/// Central meaning for work item states so callers stop open-coding <c>== Completed</c> comparisons.
/// The legacy and the work-item delivery flows disagree about which state means "done", and that
/// disagreement belongs here rather than in every handler, query and DTO that reads the column.
/// </summary>
public static class ContractWorkItemStatusExtensions
{
    /// <summary>Work is finished as far as the milestone is concerned, under either delivery flow.</summary>
    public static bool IsDelivered(this ContractWorkItemStatus status) =>
        status is ContractWorkItemStatus.Completed or ContractWorkItemStatus.Approved;

    public static bool IsDelivered(int status) => ((ContractWorkItemStatus)status).IsDelivered();

    /// <summary>The client still owes a decision on this item.</summary>
    public static bool IsAwaitingReview(this ContractWorkItemStatus status) =>
        status is ContractWorkItemStatus.Submitted;

    public static bool IsAwaitingReview(int status) => ((ContractWorkItemStatus)status).IsAwaitingReview();

    /// <summary>No further transition is possible without a dispute or an amendment.</summary>
    public static bool IsTerminal(this ContractWorkItemStatus status) =>
        status is ContractWorkItemStatus.Approved;

    public static bool IsTerminal(int status) => ((ContractWorkItemStatus)status).IsTerminal();

    /// <summary>
    /// Whether the freelancer may submit deliverables for this item in the work-item delivery flow.
    /// <see cref="ContractWorkItemStatus.Completed"/> is deliberately excluded: it only exists on rows
    /// written by the legacy flow, and those milestones never route through work-item submission.
    /// </summary>
    public static bool CanSubmit(this ContractWorkItemStatus status) =>
        status is ContractWorkItemStatus.Todo
            or ContractWorkItemStatus.InProgress
            or ContractWorkItemStatus.RevisionRequired;

    public static bool CanSubmit(int status) => ((ContractWorkItemStatus)status).CanSubmit();
}
