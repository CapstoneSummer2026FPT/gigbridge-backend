using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Premium.Freelancer.Promotions.DTOs;

public sealed record PromotionDto(
    Guid Id, string PackageId, string PackageName, decimal TokenCost,
    decimal BoostWeight, DateTime StartsAt, DateTime EndsAt,
    PromotionStatus Status, Guid? WalletTransactionId, DateTime CreatedAt,
    string PhotoUrl, string DisplayName, string? Quote, bool ShowQuote,
    string? JobTitle, bool ShowJobTitle, int ImpressionCount, int ClickCount,
    int TargetClickCount, int QueuePosition)
{
    public static PromotionDto FromEntity(FreelancerProfilePromotion promotion, int queuePosition = 0) =>
        new(promotion.FreelancerProfilePromotionsId, promotion.PackageId,
            promotion.PackageName, promotion.TokenCost, promotion.BoostWeight,
            promotion.StartTime, promotion.EndTime, promotion.Status,
            promotion.WalletTransactionId, promotion.CreatedAt, promotion.PhotoUrl,
            promotion.DisplayName, promotion.Quote, promotion.ShowQuote,
            promotion.JobTitle, promotion.ShowJobTitle, promotion.ImpressionCount,
            promotion.ClickCount, promotion.TargetClickCount, queuePosition);
}

public sealed record PromotionPolicyDto(
    int BaseTargetClicks, int TargetClicksPerCoin, decimal BoostWeightPerCoin,
    int MinimumBoostCoins, int MaximumBoostCoinsPerTransaction,
    int DisplayNameMaxLength, int QuoteMaxLength, int JobTitleMaxLength,
    int PhotoUrlMaxLength, long MaximumPhotoBytes, int VisitorKeyMaxLength,
    int DefaultFeedLimit, int MaximumFeedLimit, int InteractionDeduplicationSeconds,
    int DefaultDurationDays, int MaxQueuedCampaigns);

public sealed record PromotionDraftDto(
    string PhotoUrl, string DisplayName, string? JobTitle, PromotionPolicyDto Policy);

public sealed record PromotionManagerDto(
    PromotionDto? Active, IReadOnlyList<PromotionDto> Queued,
    IReadOnlyList<PromotionDto> History, PromotionPolicyDto Policy,
    decimal AvailableTokens);

public sealed record PublicPromotionCardDto(
    Guid Id, Guid FreelancerUserId, string PhotoUrl, string DisplayName,
    string? Quote, bool ShowQuote, string? JobTitle, bool ShowJobTitle);

public sealed record PromotionInteractionResultDto(
    Guid PromotionId, PromotionStatus Status, int ClickCount, int TargetClickCount);
