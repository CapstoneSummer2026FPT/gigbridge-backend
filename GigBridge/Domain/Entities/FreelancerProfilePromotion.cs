using Domain.Enums.Premium;

namespace Domain.Entities;

public sealed class FreelancerProfilePromotion
{
    public Guid FreelancerProfilePromotionsId { get; set; }
    public Guid FreelancerProfileId { get; set; }
    public string PackageId { get; set; } = null!;
    public string PackageName { get; set; } = null!;
    public string PurchaseIdempotencyKey { get; set; } = null!;
    public int DurationDays { get; set; }
    public decimal TokenCost { get; set; }
    public decimal BoostWeight { get; set; }
    public int QueuePosition { get; set; }
    public int TargetClickCount { get; set; }
    public string PhotoUrl { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Quote { get; set; }
    public bool ShowQuote { get; set; }
    public string? JobTitle { get; set; }
    public bool ShowJobTitle { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public PromotionStatus Status { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public int ImpressionCount { get; set; }
    public int ClickCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public FreelancerProfile FreelancerProfile { get; set; } = null!;
    public WalletTransaction? WalletTransaction { get; set; }
}
