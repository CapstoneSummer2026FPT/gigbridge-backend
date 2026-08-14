using System.Text.RegularExpressions;

namespace Application.Features.JobPosts.Common;

public static class MilestonePlanDeadlineCalculator
{
    private static readonly Regex DurationPattern = new(
        @"^\s*(\d+)\s*(day|days|ngày|ngay|week|weeks|tuần|tuan|month|months|tháng|thang|year|years|năm|nam)\s*$",
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
            "day" or "days" or "ngày" or "ngay" => 1,
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

    public static List<DateOnly?> CalculateDueDates(DateOnly? jobPostEndDate, IReadOnlyList<string?> durationsInOrder)
    {
        var result = new List<DateOnly?>(durationsInOrder.Count);
        DateOnly? start = jobPostEndDate;

        foreach (var duration in durationsInOrder)
        {
            if (start is null || !TryParseDurationDays(duration, out var days))
            {
                result.Add(null);
                start = null;
                continue;
            }

            var deadline = start.Value.AddDays(days);
            result.Add(deadline);
            start = deadline;
        }

        return result;
    }
}
