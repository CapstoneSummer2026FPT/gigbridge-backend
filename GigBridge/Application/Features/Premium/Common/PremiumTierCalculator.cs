using System.Text.Json;
using Application.Common.Exceptions;

namespace Application.Features.Premium.Common;

public sealed record PremiumTierResult(
    string Name,
    int Threshold,
    string? NextName,
    int? NextThreshold,
    decimal Progress);

public static class PremiumTierCalculator
{
    public const string SettingKey = "premium.freelancer.tier-thresholds";

    public static PremiumTierResult Calculate(int points, string? settingValue)
    {
        var thresholds = string.IsNullOrWhiteSpace(settingValue)
            ? new Dictionary<string, int>
            {
                ["Bronze"] = 0,
                ["Silver"] = 500,
                ["Gold"] = 1500,
                ["Platinum"] = 3000
            }
            : Parse(settingValue);

        var ordered = thresholds.OrderBy(item => item.Value).ToList();
        var index = ordered.FindLastIndex(item => points >= item.Value);
        if (index < 0)
            index = 0;

        var current = ordered[index];
        var next = index + 1 < ordered.Count ? ordered[index + 1] : default;
        var progress = next.Key is null
            ? 1m
            : Math.Clamp((decimal)(points - current.Value) / (next.Value - current.Value), 0m, 1m);

        return new PremiumTierResult(
            current.Key,
            current.Value,
            next.Key,
            next.Key is null ? null : next.Value,
            progress);
    }

    private static Dictionary<string, int> Parse(string value)
    {
        try
        {
            var result = JsonSerializer.Deserialize<Dictionary<string, int>>(value);
            if (result is null ||
                result.Count == 0 ||
                result.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value < 0) ||
                result.Values.Distinct().Count() != result.Count ||
                result.Values.Min() != 0)
                throw new BadRequestException($"Platform setting '{SettingKey}' is invalid.");
            return result;
        }
        catch (JsonException exception)
        {
            throw new BadRequestException(
                $"Platform setting '{SettingKey}' is not valid JSON: {exception.Message}");
        }
    }
}
