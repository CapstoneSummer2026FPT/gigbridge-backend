using System.Text.RegularExpressions;

namespace Application.Features.JobPosts.Common;

public static class MilestonePlanDeadlineCalculator
{
    private static readonly Regex DurationPattern = new(
        @"^\s*(\d+)\s*(week|weeks|tuần|tuan|month|months|tháng|thang|year|years|năm|nam)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    public static bool TryParseDurationDays(string? duration, out int days)
    {
        days = 0;
        if (string.IsNullOrWhiteSpace(duration))
        {
            return false;
        }

        var match = DurationPattern.Match(duration);
        if (!match.Success)
        {
            return false;
        }

        var amount = int.Parse(match.Groups[1].Value);
        if (amount <= 0)
        {
            return false;
        }

        var unit = match.Groups[2].Value.ToLowerInvariant();
        var daysPerUnit = unit switch
        {
            "week" or "weeks" or "tuần" or "tuan" => DaysPerWeek,
            "month" or "months" or "tháng" or "thang" => DaysPerMonth,
            "year" or "years" or "năm" or "nam" => DaysPerYear,
            _ => (int?)null
        };

        if (daysPerUnit is null)
        {
            return false;
        }

        days = amount * daysPerUnit.Value;
        return true;
    }

    // Each stage starts the day after the previous one ends: Milestone 1 starts the day
    // after the JobPost's end date, and Milestone N+1 starts the day after Milestone N's
    // deadline — work never starts on the same calendar day the prior stage ends.
    public static List<DateOnly?> CalculateDueDates(DateOnly? jobPostEndDate, IReadOnlyList<string?> durationsInOrder)
    {
        var result = new List<DateOnly?>(durationsInOrder.Count);
        DateOnly? nextStart = jobPostEndDate?.AddDays(1);

        foreach (var duration in durationsInOrder)
        {
            if (nextStart is null || !TryParseDurationDays(duration, out var days))
            {
                result.Add(null);
                nextStart = null;
                continue;
            }

            // The start day itself counts as day 1 of the duration, so the deadline is
            // `days - 1` after the start (e.g. a 7-day span starting Aug 2 ends Aug 8).
            var deadline = nextStart.Value.AddDays(days - 1);
            result.Add(deadline);
            nextStart = deadline.AddDays(1);
        }

        return result;
    }
}
