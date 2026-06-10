using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class SubscriptionPlan
{
    public Guid SubscriptionPlansId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? Currency { get; set; }

    public int DurationInDays { get; set; }

    public string? Features { get; set; }

    /// <summary>
    /// Enum UserRole: 0=Client, 1=Freelancer, NULL=Both
    /// </summary>
    public int? TargetRole { get; set; }

    public bool? IsActive { get; set; }

    public int? SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
