using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.Contracts.Milestones;

namespace Application.Features.Disputes.Common.Internal;

/// <summary>
/// Single source of truth for the "Create Job Post from Remaining Milestones" feature:
/// which milestones count as unfinished, and the duration math used to derive the new
/// Job Post's deadline. Shared by the /disputes/my list flag and the remaining-job-post
/// plan endpoint so the two can never disagree about eligibility or milestone selection.
/// </summary>
public static class DisputeJobPostRecreationSupport
{
    private const decimal WeeksPerMonth = 4.333m;
    private const decimal WeeksPerYear = 52m;

    /// <summary>
    /// A contract only becomes eligible once it was terminated via "Cancel Contract
    /// Immediately" (AdminContractAction.Terminate), and only while at least one milestone
    /// was left unfinished — i.e. not already Approved/Completed before the dispute.
    /// </summary>
    public static bool IsEligible(int contractStatus, IEnumerable<Milestone> milestones) =>
        contractStatus == (int)ContractStatus.Cancelled && milestones.Any(IsUnfinished);

    public static List<Milestone> GetRemainingMilestones(IEnumerable<Milestone> milestones) =>
        milestones.Where(IsUnfinished)
            .OrderBy(milestone => milestone.SortOrder)
            .ThenBy(milestone => milestone.CreatedAt)
            .ToList();

    private static bool IsUnfinished(Milestone milestone) =>
        milestone.Status is not ((int)MilestoneStatus.Approved or (int)MilestoneStatus.Completed);

    /// <summary>
    /// Parses the canonical "&lt;n&gt; week(s)/month(s)/year(s)" format produced by the
    /// frontend's formatJobDuration (jobDuration.ts) into a week count. Returns null if the
    /// string is missing or not in that format, so callers can fall back to a proportional
    /// share of the original Job Post's estimated duration.
    /// </summary>
    public static decimal? ParseWeeksOrNull(string? estimatedDuration)
    {
        if (string.IsNullOrWhiteSpace(estimatedDuration)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            estimatedDuration.Trim(),
            @"^(\d+)\s*(weeks?|months?|years?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var value = decimal.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLowerInvariant();
        return unit switch
        {
            var u when u.StartsWith("week") => value,
            var u when u.StartsWith("month") => value * WeeksPerMonth,
            var u when u.StartsWith("year") => value * WeeksPerYear,
            _ => null
        };
    }

    /// <summary>
    /// Formats a week count back into the canonical duration string, rounding up to whole
    /// weeks (a duration estimate should never understate remaining work).
    /// </summary>
    public static string FormatWeeks(decimal weeks)
    {
        var wholeWeeks = Math.Max(1, (int)Math.Ceiling(weeks));
        return wholeWeeks == 1 ? "1 week" : $"{wholeWeeks} weeks";
    }
}
