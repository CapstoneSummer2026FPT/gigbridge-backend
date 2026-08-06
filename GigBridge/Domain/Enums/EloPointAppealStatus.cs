namespace Domain.Enums;

/// <summary>Lifecycle of an Elo appeal submitted by a user against a transaction.</summary>
public enum EloPointAppealStatus
{
    Pending = 0,
    UnderReview = 1,
    Approved = 2,
    PartiallyApproved = 3,
    Rejected = 4,
    Cancelled = 5
}
