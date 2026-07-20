using System.Globalization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Premium.Client.JobPostPromotion.DTOs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.JobPostPromotion.Common;

public static class JobPromotionPolicy
{
    public const string TokenCostKey = "premium.client.job-promotion.token-cost";
    public const string DurationDaysKey = "premium.client.job-promotion.duration-days";
    public static readonly JobPromotionPolicyDto Defaults = new(10m, 7);

    public static async Task<JobPromotionPolicyDto> LoadAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var values = await context.Set<PlatformSetting>()
            .AsNoTracking()
            .Where(x => x.Key == TokenCostKey || x.Key == DurationDaysKey)
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        var tokenCost = values.TryGetValue(TokenCostKey, out var tokenValue)
            ? ParseTokenCost(tokenValue)
            : Defaults.TokenCost;
        var durationDays = values.TryGetValue(DurationDaysKey, out var durationValue)
            ? ParseDuration(durationValue)
            : Defaults.DurationDays;
        return new JobPromotionPolicyDto(tokenCost, durationDays);
    }

    public static void Validate(decimal tokenCost, int durationDays)
    {
        if (tokenCost <= 0 || tokenCost != decimal.Truncate(tokenCost))
            throw new BadRequestException("Promotion token cost must be a positive whole-coin value.");
        if (durationDays is < 1 or > 365)
            throw new BadRequestException("Promotion duration must be between 1 and 365 days.");
    }

    private static decimal ParseTokenCost(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            throw new BadRequestException($"Platform setting '{TokenCostKey}' is invalid.");
        Validate(result, Defaults.DurationDays);
        return result;
    }

    private static int ParseDuration(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new BadRequestException($"Platform setting '{DurationDaysKey}' is invalid.");
        Validate(Defaults.TokenCost, result);
        return result;
    }
}
