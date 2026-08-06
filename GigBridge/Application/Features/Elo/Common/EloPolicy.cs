using System.Globalization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Elo.DTOs;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Elo.Common;

/// <summary>
/// Reads/validates the platform-wide Elo policy stored as PlatformSetting rows,
/// mirroring the JobPromotionPolicy/PromotionPolicy pattern. Defaults to a 50%
/// dispute-resolution penalty when no rows are configured.
/// </summary>
public static class EloPolicy
{
    public const string DisputePenaltyModeKey = "elo.dispute-penalty-mode";
    public const string DisputePenaltyValueKey = "elo.dispute-penalty-value";

    public static readonly EloPolicyDto Defaults = new(EloAdjustmentMode.Percentage, 50m);

    public static async Task<EloPolicyDto> LoadAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var values = await context.Set<PlatformSetting>()
            .AsNoTracking()
            .Where(x => x.Key == DisputePenaltyModeKey || x.Key == DisputePenaltyValueKey)
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        var mode = values.TryGetValue(DisputePenaltyModeKey, out var modeValue)
            ? ParseMode(modeValue)
            : Defaults.Mode;
        var value = values.TryGetValue(DisputePenaltyValueKey, out var valueValue)
            ? ParseValue(valueValue)
            : Defaults.Value;
        return new EloPolicyDto(mode, value);
    }

    public static void Validate(EloAdjustmentMode mode, decimal value)
    {
        if (mode == EloAdjustmentMode.Percentage && (value <= 0 || value > 100))
        {
            throw new BadRequestException("Dispute penalty percentage must be between 1 and 100.");
        }

        if (mode == EloAdjustmentMode.FixedPoints && (value <= 0 || value != decimal.Truncate(value)))
        {
            throw new BadRequestException("Dispute penalty fixed points must be a positive whole number.");
        }
    }

    private static EloAdjustmentMode ParseMode(string value)
    {
        return value switch
        {
            "Percentage" => EloAdjustmentMode.Percentage,
            "FixedPoints" => EloAdjustmentMode.FixedPoints,
            _ => throw new BadRequestException($"Platform setting '{DisputePenaltyModeKey}' is invalid.")
        };
    }

    private static decimal ParseValue(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            throw new BadRequestException($"Platform setting '{DisputePenaltyValueKey}' is invalid.");
        }

        return result;
    }
}
