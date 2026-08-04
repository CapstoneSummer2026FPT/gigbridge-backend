using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class AdminAuditLog
{
    public Guid AdminAuditLogsId { get; set; }

    public Guid AdminId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? EntityId { get; set; }

    public string? EntityType { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public Guid CorrelationId { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Admin { get; set; } = null!;
}
