using System.Text.RegularExpressions;

namespace Application.Common.InternalServices.Scheduling;

public static class MilestoneDeadlineCalculator
{
    private static readonly Regex DurationPattern = new(
        @"^\s*(\d+)\s*(week|weeks|tuần|tuan|month|months|tháng|thang|year|years|năm|nam)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WorkItemDurationPattern = new(
        @"^\s*(\d+)\s*(day|days|ngày|ngay|week|weeks|tuần|tuan|month|months|tháng|thang|year|years|năm|nam)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int DaysPerDay = 1;
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

    // Work items are allowed a finer duration grain (day(s)) than milestones, which stay
    // week(s)+ only. This is a deliberately separate parser from TryParseDurationDays so
    // milestone-level duration validity never accepts a day-only value.
    public static bool TryParseWorkItemDurationDays(string? duration, out int days)
    {
        days = 0;
        if (string.IsNullOrWhiteSpace(duration))
        {
            return false;
        }

        var match = WorkItemDurationPattern.Match(duration);
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
            "day" or "days" or "ngày" or "ngay" => DaysPerDay,
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

    // Compares the sum of a milestone's work-item durations against the milestone's own
    // duration. Returns false (no result / safe no-op) whenever the milestone duration
    // itself isn't set or isn't parseable yet, so in-progress drafts never false-positive.
    // Work items with a blank/unparseable duration contribute 0 days rather than failing
    // the comparison, since per-item duration is optional.
    public static bool TryGetWorkItemDurationOverage(
        string? milestoneDuration,
        IEnumerable<string?> workItemDurations,
        out int totalWorkItemDays,
        out int milestoneDays,
        out int overageDays)
    {
        totalWorkItemDays = 0;
        milestoneDays = 0;
        overageDays = 0;

        if (!TryParseDurationDays(milestoneDuration, out milestoneDays))
        {
            return false;
        }

        foreach (var workItemDuration in workItemDurations)
        {
            if (TryParseWorkItemDurationDays(workItemDuration, out var itemDays))
            {
                totalWorkItemDays += itemDays;
            }
        }

        overageDays = Math.Max(0, totalWorkItemDays - milestoneDays);
        return true;
    }

    // Each stage starts the day after the previous one ends: Milestone 1 starts the day
    // after the anchor date (JobPost end date, proposal's job closing date, or "today" for
    // negotiations), and Milestone N+1 starts the day after Milestone N's deadline — work
    // never starts on the same calendar day the prior stage ends.
    public static List<DateOnly?> CalculateDueDates(DateOnly? anchorDate, IReadOnlyList<string?> durationsInOrder)
    {
        var result = new List<DateOnly?>(durationsInOrder.Count);
        DateOnly? nextStart = anchorDate?.AddDays(1);

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
