namespace Domain.Enums;

/// <summary>
/// Matches the existing Dispute.Status int column in the database.
/// 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed
/// Old values (pre-migration): 0=Open, 1=UnderReview, 2=Resolved, 3=Closed
/// </summary>
public enum DisputeStatus
{
    Open = 0,
    WaitingAdmin = 1,
    UnderReview = 2,
    WaitingEvidence = 3,
    DecisionPending = 4,
    Resolved = 5,
    Closed = 6
}
