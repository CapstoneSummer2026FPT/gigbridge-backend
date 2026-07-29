using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Freelancer.Promotions.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Freelancer.Promotions.Common;

public static class PromotionPolicy
{
    // All promotion queue mutations use this transaction-scoped database lock.
    // The stable value is arbitrary, but must remain identical across application instances.
    public const long QueueTransactionLockKey = 0x4769674272696467;
    public const string SettingKey = "premium.freelancer.promotion-policy";
    public const string CachePrefix = "premium:promotion:";
    public const string FeedCacheKey = "premium:promotions:feed";
    public const string CustomCampaignId = "custom";
    public const string CustomCampaignName = "Custom promotion";

    public static readonly PromotionPolicyDto Defaults = new(
        BaseTargetClicks: 40, TargetClicksPerCoin: 10, BoostWeightPerCoin: 1m,
        MinimumBoostCoins: 1, MaximumBoostCoinsPerTransaction: 1000,
        DisplayNameMaxLength: 120, QuoteMaxLength: 240, JobTitleMaxLength: 160,
        PhotoUrlMaxLength: 2048, MaximumPhotoBytes: 5_242_880,
        VisitorKeyMaxLength: 128, DefaultFeedLimit: 12, MaximumFeedLimit: 50,
        InteractionDeduplicationSeconds: 60, DefaultDurationDays: 7,
        MaxQueuedCampaigns: 3);

    public static async Task<PromotionPolicyDto> LoadAsync(
        IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var json = await context.Set<PlatformSetting>().AsNoTracking()
            .Where(x => x.Key == SettingKey).Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return Defaults;
        try
        {
            using var document = JsonDocument.Parse(json);
            var value = JsonSerializer.Deserialize<PromotionPolicyDto>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (value is not null)
                value = value with
                {
                    DefaultDurationDays = document.RootElement.TryGetProperty("defaultDurationDays", out _)
                        ? value.DefaultDurationDays : Defaults.DefaultDurationDays,
                    MaxQueuedCampaigns = document.RootElement.TryGetProperty("maxQueuedCampaigns", out _)
                        ? value.MaxQueuedCampaigns : Defaults.MaxQueuedCampaigns
                };
            if (value is null || value.BaseTargetClicks < 0 || value.TargetClicksPerCoin <= 0 ||
                value.BoostWeightPerCoin <= 0 || value.MinimumBoostCoins <= 0 ||
                value.MaximumBoostCoinsPerTransaction < value.MinimumBoostCoins ||
                value.DisplayNameMaxLength <= 0 || value.QuoteMaxLength <= 0 ||
                value.JobTitleMaxLength <= 0 || value.PhotoUrlMaxLength <= 0 ||
                value.MaximumPhotoBytes <= 0 || value.VisitorKeyMaxLength <= 0 ||
                value.DefaultFeedLimit <= 0 || value.MaximumFeedLimit < value.DefaultFeedLimit ||
                value.InteractionDeduplicationSeconds <= 0 || value.DefaultDurationDays <= 0 ||
                value.MaxQueuedCampaigns < 0)
                throw new BadRequestException($"Platform setting '{SettingKey}' is invalid.");
            return value;
        }
        catch (JsonException exception)
        {
            throw new BadRequestException($"Platform setting '{SettingKey}' is invalid: {exception.Message}");
        }
    }

    public static int TargetClicks(decimal cumulativeTokens, PromotionPolicyDto policy) =>
        checked(policy.BaseTargetClicks + decimal.ToInt32(cumulativeTokens * policy.TargetClicksPerCoin));

    public static decimal BoostWeight(decimal cumulativeTokens, PromotionPolicyDto policy) =>
        cumulativeTokens * policy.BoostWeightPerCoin;

    public static async Task RecalculateQueuePositionsAsync(
        IApplicationDbContext context,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var campaigns = await context.Set<FreelancerProfilePromotion>()
            .Where(item => item.QueuePosition > 0 ||
                           (item.Status == Domain.Enums.PromotionStatus.Active &&
                            item.StartTime <= now &&
                            item.EndTime > now))
            .ToListAsync(cancellationToken);

        var ordered = OrderQueue(campaigns, now);
        foreach (var campaign in campaigns)
            campaign.QueuePosition = 0;
        await context.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < ordered.Count; index++)
            ordered[index].QueuePosition = index + 1;

        await context.SaveChangesAsync(cancellationToken);
    }

    public static IReadOnlyList<FreelancerProfilePromotion> OrderQueue(
        IEnumerable<FreelancerProfilePromotion> campaigns,
        DateTime now) =>
        campaigns
            .Where(item => item.Status == Domain.Enums.PromotionStatus.Active &&
                           item.StartTime <= now &&
                           item.EndTime > now)
            .OrderByDescending(item => item.BoostWeight)
            .ThenBy(item => item.QueuePosition <= 0
                ? int.MaxValue
                : item.QueuePosition)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.FreelancerProfilePromotionsId)
            .ToList();

    public static string UserCacheKey(Guid userId) => $"{CachePrefix}{userId:N}";
}
