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

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
