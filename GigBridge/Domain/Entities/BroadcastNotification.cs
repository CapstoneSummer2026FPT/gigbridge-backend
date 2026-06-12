using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class BroadcastNotification
{
    public Guid BroadcastNotificationId { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public int Type { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? ReferenceType { get; set; }

    public int TargetScope { get; set; }

    public int? TargetRole { get; set; }

    public Guid? CreatedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual User? CreatedByAdmin { get; set; }

    public virtual ICollection<BroadcastNotificationRecipient> Recipients { get; set; } = new List<BroadcastNotificationRecipient>();
}
