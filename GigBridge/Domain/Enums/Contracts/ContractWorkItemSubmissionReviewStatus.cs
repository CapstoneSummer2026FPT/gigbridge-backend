namespace Domain.Enums.Contracts;

/// <summary>
/// The client's verdict on one work item submission attempt. Transitions once, from
/// <see cref="Submitted"/> to either <see cref="Approved"/> or <see cref="RevisionRequired"/>;
/// a resubmission creates a new attempt rather than reopening this one.
/// </summary>
public enum ContractWorkItemSubmissionReviewStatus
{
    Submitted = 0,
    Approved = 1,
    RevisionRequired = 2
}
