using Domain.Enums;

namespace Domain.Entities;

public sealed class PremiumUsageEvent
{
    public Guid PremiumUsageEventId { get; set; }
    public PremiumUsageEventType Type { get; set; }
    public Guid? UserId { get; set; }
    public Guid? JobPostId { get; set; }
    public Guid? PromotionId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Metadata { get; set; }

    public User? User { get; set; }
    public JobPost? JobPost { get; set; }
}
