using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class UserEloScore
{
    public Guid UserEloScoresId { get; set; }

    public Guid UserId { get; set; }

    public int CurrentPoints { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime? LastInactivityPenaltyAt { get; set; }

    public DateTime? LastReturnBonusAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
