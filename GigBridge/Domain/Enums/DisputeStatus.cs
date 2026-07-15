namespace Domain.Enums;

/// <summary>
/// Matches the existing Dispute.Status int column in the database.
/// 0=Open, 1=UnderReview, 2=Resolved, 3=Closed
/// </summary>
public enum DisputeStatus
{
    Open = 0,
    UnderReview = 1,
    Resolved = 2,
    Closed = 3
}
