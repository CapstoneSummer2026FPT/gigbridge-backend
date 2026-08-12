namespace Domain.Enums.Disputes;

/// <summary>
/// Matches the Disputes.Status int column in the database.
/// 0=WaitingAdmin, 1=InProgress, 2=Resolved, 3=Closed
/// </summary>
public enum DisputeStatus
{
    WaitingAdmin = 0,
    InProgress   = 1,
    Resolved     = 2,
    Closed       = 3
}
