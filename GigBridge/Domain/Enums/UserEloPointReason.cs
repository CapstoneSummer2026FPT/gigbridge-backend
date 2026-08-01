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
    ReviewModeration = 6
}
