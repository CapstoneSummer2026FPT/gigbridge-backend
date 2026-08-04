using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class Notification
{
    public Guid NotificationsId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired, 20=ReportUpdate
    /// </summary>
    public int Type { get; set; }

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? ReferenceType { get; set; }

    public string? Metadata { get; set; }

    public int? Revision { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
