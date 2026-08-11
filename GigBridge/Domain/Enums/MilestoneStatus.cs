namespace Domain.Enums;

public enum MilestoneStatus
{
    Pending = 0,
    InProgress = 1,
    Submitted = 2,
    Approved = 3,
    [Obsolete("Payment state is derived from milestone ReleasedAmount and contract escrow status.")]
    PaymentProofUploaded = 4,
    [Obsolete("Payment state is derived from milestone ReleasedAmount and contract escrow status.")]
    PaymentConfirmed = 5,
    Disputed = 6,
    Cancelled = 7,
    Completed = 8
}
