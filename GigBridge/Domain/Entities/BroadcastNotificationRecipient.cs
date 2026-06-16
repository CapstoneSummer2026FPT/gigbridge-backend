using System;

namespace Domain.Entities;

public partial class BroadcastNotificationRecipient
{
    public Guid BroadcastNotificationRecipientId { get; set; }

    public Guid BroadcastNotificationId { get; set; }

    public Guid UserId { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual BroadcastNotification BroadcastNotification { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
