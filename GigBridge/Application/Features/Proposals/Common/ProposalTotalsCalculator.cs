using System.Text.RegularExpressions;
using Application.Features.Proposals.Common.DTOs;

namespace Application.Features.Proposals.Common;

public static partial class ProposalTotalsCalculator
{
    private const decimal MaxProposalAmount = 9999999999999999.99m;

    public static decimal? ResolveBudget(
        decimal? requestedBudget,
        IEnumerable<ProposalMilestonePlanDto> milestones)
    {
        if (requestedBudget.HasValue) return requestedBudget.Value;

        var total = milestones.Sum(item => item.Amount);
        return total > 0 ? decimal.Round(total, 2, MidpointRounding.AwayFromZero) : null;
    }

    public static string? ResolveDuration(
        string? requestedDuration,
        IEnumerable<ProposalMilestonePlanDto> milestones)
    {
        var cleaned = ProposalPlanMapper.Clean(requestedDuration);
        return cleaned ?? CalculateDuration(milestones.Select(item => item.EstimatedDuration));
    }

    public static string? CalculateDuration(IEnumerable<string?> durations)
    {
        var parsed = durations
            .Select(TryParseDuration)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();

        if (parsed.Count == 0) return null;

        var outputUnit = parsed.MaxBy(item => item.Unit.Rank).Unit;
        var totalDays = parsed.Sum(item => item.Amount * item.Unit.Days);
        var roundedAmount = (int)Math.Ceiling((decimal)totalDays / outputUnit.Days);
        var label = roundedAmount == 1 ? outputUnit.Singular : outputUnit.Plural;
        return $"{roundedAmount} {label}";
    }

    public static bool IsValidDuration(string? value) => TryParseDuration(value).HasValue;

    public static bool IsValidAmount(decimal value) =>
        value > 0 && value <= MaxProposalAmount && decimal.Round(value, 2) == value;

    public static bool IsValidDraftAmount(decimal value) =>
        value >= 0 && value <= MaxProposalAmount && decimal.Round(value, 2) == value;

    private static ParsedDuration? TryParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var match = DurationPattern().Match(value);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var amount) || amount <= 0)
        {
            return null;
        }

        var unit = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "day" or "days" => new DurationUnit("day", "days", 1, 0),
            "week" or "weeks" => new DurationUnit("week", "weeks", 7, 1),
            "month" or "months" => new DurationUnit("month", "months", 30, 2),
            _ => default
        };

        return unit.Days == 0 ? null : new ParsedDuration(amount, unit);
    }

    [GeneratedRegex(@"^\s*(\d+)\s*(day|days|week|weeks|month|months)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPattern();

    private readonly record struct ParsedDuration(int Amount, DurationUnit Unit);
    private readonly record struct DurationUnit(string Singular, string Plural, int Days, int Rank);
}
