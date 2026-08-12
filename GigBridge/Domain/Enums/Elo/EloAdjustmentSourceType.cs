namespace Domain.Enums.Elo;

/// <summary>
/// Origin of an Elo adjustment. Recorded on UserEloPointTransaction.SourceType
/// so the user Elo history UI can group changes by source. Legacy rows (written
/// before this column existed) have a null SourceType and are treated as System.
/// </summary>
public enum EloAdjustmentSourceType
{
    Review = 0,
    Dispute = 1,
    EloAppeal = 2,
    Admin = 3,
    System = 4
}
