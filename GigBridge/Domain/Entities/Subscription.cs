using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Domain.Entities;

public partial class Subscription
{
    public Guid SubscriptionsId { get; set; }

    public Guid UserId { get; set; }

    public Guid SubscriptionPlansId { get; set; }

    /// <summary>
    /// Enum SubscriptionStatus: 0=Active, 1=Expired, 2=Cancelled
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool? AutoRenew { get; set; }

    public string? PaymentReference { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual SubscriptionPlan SubscriptionPlans { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
