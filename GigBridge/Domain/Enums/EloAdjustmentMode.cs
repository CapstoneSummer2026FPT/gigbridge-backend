namespace Domain.Enums;

/// <summary>
/// How an Elo adjustment amount is interpreted. FixedPoints deducts/adds an exact
/// number of points; Percentage computes a share of the user's current points
/// (rounded half-up). Stored on UserEloPointTransaction.Mode (null = FixedPoints).
/// </summary>
public enum EloAdjustmentMode
{
    FixedPoints = 0,
    Percentage = 1
}
