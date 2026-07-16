namespace Application.Features.Premium.Client.JobPostPromotion.DTOs;

public sealed record PromoteJobPostRequest(string IdempotencyKey);

public sealed record JobPostPromotionDto(
    Guid JobPostId,
    bool IsFeatured,
    DateTime FeaturedFrom,
    DateTime FeaturedUntil,
    decimal TokenCost,
    Guid WalletTransactionId);

public sealed record JobPromotionPolicyDto(decimal TokenCost, int DurationDays);

public sealed record UpdateJobPromotionPolicyRequest(decimal TokenCost, int DurationDays);
