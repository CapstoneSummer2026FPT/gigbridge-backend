namespace Domain.Enums.Elo;

public enum UserEloPointReason
{
    InitialGrant = 0,
    InactivityPenalty = 1,
    ReturnBonus = 2,
    JobCompletion = 3,
    ReviewRating = 4,

    // Persisted ledger value from the retired integrity-monitoring workflow.
    // Never reuse or renumber this value.
    LegacyIntegrityPenalty = 5,
    ReviewModeration = 6,

    /// <summary>
    /// Single Elo delta applied once when a job/contract reaches Completed and a
    /// valid final review is recorded. One transaction per (reviewee, contract).
    /// </summary>
    CompletedJobReview = 7,

    /// <summary>
    /// Elo penalty applied to a party flagged as violating platform rules when an
    /// administrator resolves a dispute. Deducts the configured penalty (default
    /// 50% of current points, rounded half-up). One transaction per (dispute, user).
    /// </summary>
    DisputeResolutionPenalty = 8,

    /// <summary>
    /// Manual Elo increase granted by an administrator through the centralized
    /// adjustment workflow. Positive PointsDelta.
    /// </summary>
    AdminIncrease = 9,

    /// <summary>
    /// Manual Elo decrease applied by an administrator through the centralized
    /// adjustment workflow. Negative PointsDelta.
    /// </summary>
    AdminDecrease = 10,

    /// <summary>
    /// Correction transaction written when an Elo appeal is resolved (full reversal,
    /// partial correction, or custom adjustment). May be positive or negative.
    /// </summary>
    AppealCorrection = 11,

    /// <summary>
    /// Reversal of a prior Elo change that was applied by mistake (e.g. restored
    /// review Elo). Kept distinct from AppealCorrection for admin/audit clarity.
    /// </summary>
    Reversal = 12,

    /// <summary>
    /// Generic system-driven adjustment that does not map to a named workflow.
    /// </summary>
    SystemAdjustment = 13
}
