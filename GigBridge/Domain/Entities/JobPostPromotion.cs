namespace Domain.Entities;

public sealed class JobPostPromotion
{
    public Guid JobPostPromotionsId { get; set; }
    public Guid JobPostId { get; set; }
    public Guid ClientUserId { get; set; }
    public Guid WalletTransactionId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal TokenCost { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string PromotionTitle { get; set; } = string.Empty;
    public string PromotionDescription { get; set; } = string.Empty;
    public int ImpressionCount { get; set; }
    public int ClickCount { get; set; }
    public DateTime FeaturedFrom { get; set; }
    public DateTime FeaturedUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    public JobPost JobPost { get; set; } = null!;
    public User ClientUser { get; set; } = null!;
    public WalletTransaction WalletTransaction { get; set; } = null!;
}
