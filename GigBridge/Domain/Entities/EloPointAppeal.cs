using System;

namespace Domain.Entities;

/// <summary>
/// Appeal submitted by a user against a single Elo point transaction, requesting
/// that an administrator review and correct the applied change. At most one
/// active appeal (Pending/UnderReview) may exist per transaction.
/// </summary>
public partial class EloPointAppeal
{
    public Guid EloPointAppealId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The Elo transaction being appealed.</summary>
    public Guid EloPointTransactionId { get; set; }

    /// <summary>Enum EloPointAppealStatus: Pending/UnderReview/Approved/PartiallyApproved/Rejected/Cancelled.</summary>
    public int Status { get; set; }

    /// <summary>Enum EloPointAppealResolution once resolved; null while pending.</summary>
    public int? Resolution { get; set; }

    /// <summary>User's stated reason for the appeal.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Admin note recorded when the appeal is resolved.</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Points delta the admin chose for PartialCorrection/CustomAdjustment.
    /// Ignored for FullReversal (uses the negated original delta) and NoChange.
    /// </summary>
    public int? CorrectedDelta { get; set; }

    /// <summary>The correction transaction written when this appeal is resolved.</summary>
    public Guid? AppliedTransactionId { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? CancelledById { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual UserEloPointTransaction EloPointTransaction { get; set; } = null!;

    public virtual UserEloPointTransaction? AppliedTransaction { get; set; }

    public virtual User? ReviewedByAdmin { get; set; }

    public virtual ICollection<EloPointAppealEvidence> Evidence { get; set; } = new List<EloPointAppealEvidence>();
}
