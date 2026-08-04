using System;

namespace Domain.Entities;

public partial class UserEloPointTransaction
{
    public Guid UserEloPointTransactionsId { get; set; }

    public Guid UserId { get; set; }

    public int PointsDelta { get; set; }

    public int PointsBefore { get; set; }

    public int PointsAfter { get; set; }

    public int Reason { get; set; }

    public string? SourceEntityType { get; set; }

    public Guid? SourceEntityId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public string? Metadata { get; set; }

    /// <summary>Completed job whose review drove this Elo change (Reason = CompletedJobReview).</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Review that drove this Elo change (Reason = CompletedJobReview).</summary>
    public Guid? ReviewId { get; set; }

    /// <summary>Final review rating (1.0–5.0, one decimal place) for completed-job review changes.</summary>
    public decimal? Rating { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
