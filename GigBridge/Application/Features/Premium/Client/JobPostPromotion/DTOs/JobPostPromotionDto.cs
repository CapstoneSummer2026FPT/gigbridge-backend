namespace Application.Features.Premium.Client.JobPostPromotion.DTOs;

public sealed record PromoteJobPostRequest(
    string IdempotencyKey,
    string ImageUrl,
    string PromotionTitle,
    string PromotionDescription);

public sealed record JobPostPromotionDto(
    Guid JobPostId,
    bool IsFeatured,
    DateTime FeaturedFrom,
    DateTime FeaturedUntil,
    decimal TokenCost,
    Guid WalletTransactionId,
    Guid PromotionId,
    string ImageUrl,
    string PromotionTitle,
    string PromotionDescription);

public sealed record PublicJobPromotionCardDto(
    Guid Id,
    Guid JobPostId,
    string ImageUrl,
    string Title,
    string Description,
    DateTime FeaturedUntil);

public sealed record JobPromotionInteractionDto(Guid Id, int ImpressionCount, int ClickCount);

public sealed record JobPromotionPolicyDto(decimal TokenCost, int DurationDays);

public sealed record UpdateJobPromotionPolicyRequest(decimal TokenCost, int DurationDays);
