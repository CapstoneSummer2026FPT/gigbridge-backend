namespace Domain.Enums;

/// <summary>
/// Admin decision recorded when an Elo appeal is resolved. Drives the correction
/// transaction created by the centralized Elo workflow.
/// </summary>
public enum EloPointAppealResolution
{
    NoChange = 0,
    FullReversal = 1,
    PartialCorrection = 2,
    CustomAdjustment = 3
}
