namespace Domain.Enums;

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
    /// administrator resolves a dispute. Deducts 50% of current points (rounded
    /// half-up). One transaction per (dispute, user).
    /// </summary>
    DisputeResolutionPenalty = 8
}
