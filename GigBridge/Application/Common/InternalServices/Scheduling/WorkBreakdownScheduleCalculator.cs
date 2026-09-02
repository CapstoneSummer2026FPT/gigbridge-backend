namespace Application.Common.InternalServices.Scheduling;

public enum WbsValidationCode
{
    Valid = 0,
    NoWorkItems = 1,
    MilestoneDurationInvalid = 2,
    SingleWeekRequiresExactlyOneItem = 3,
    SingleWeekItemDurationMismatch = 4,
    WorkItemDurationInvalid = 5,
    TotalDurationExceedsMilestone = 6
}

/// <param name="WorkItemIndex">Zero-based index of the offending work item, or -1 when the failure is about the milestone as a whole.</param>
public readonly record struct WbsValidationResult(WbsValidationCode Code, int WorkItemIndex)
{
    public bool IsValid => Code == WbsValidationCode.Valid;

    public static WbsValidationResult Valid() => new(WbsValidationCode.Valid, -1);

    public static WbsValidationResult Invalid(WbsValidationCode code, int workItemIndex = -1) =>
        new(code, workItemIndex);
}

/// <summary>
/// Schedules and validates the work breakdown structure hanging off a single milestone.
///
/// Deadlines deliberately reuse <see cref="MilestoneDeadlineCalculator"/> rather than repeating the date
/// arithmetic: a milestone's work items chain exactly the way milestones themselves do, one level down.
/// The anchor for milestone N's work items is that milestone's own start minus one day — which is the
/// previous milestone's deadline — so work item 1 starts the same day the milestone starts, and the last
/// work item lands on the milestone's own deadline when the durations add up exactly.
/// </summary>
public static class WorkBreakdownScheduleCalculator
{
    // A milestone of one week or less cannot meaningfully be broken down, so it carries exactly one work item.
    private const int SingleWeekDays = 7;

    public static List<DateOnly?> CalculateWorkItemDueDates(
        DateOnly? milestoneStartAnchor,
        IReadOnlyList<string?> durationsInOrder) =>
        MilestoneDeadlineCalculator.CalculateDueDates(milestoneStartAnchor, durationsInOrder);

    /// <summary>
    /// Resolves the anchor to pass to <see cref="CalculateWorkItemDueDates"/> for one milestone.
    /// Call this per milestone — never run a single chain across a whole contract, because
    /// <see cref="MilestoneDeadlineCalculator.CalculateDueDates"/> nulls every remaining entry after the first
    /// unparseable duration, and one bad work item would otherwise poison later milestones too.
    /// </summary>
    public static DateOnly? ResolveMilestoneStartAnchor(
        DateOnly? planAnchor,
        IReadOnlyList<DateOnly?> milestoneDueDatesInOrder,
        int milestoneIndex)
    {
        if (milestoneIndex <= 0)
        {
            return planAnchor;
        }

        return milestoneIndex <= milestoneDueDatesInOrder.Count
            ? milestoneDueDatesInOrder[milestoneIndex - 1]
            : null;
    }

    public static WbsValidationResult Validate(
        string? milestoneDuration,
        IReadOnlyList<string?> workItemDurations)
    {
        if (workItemDurations.Count == 0)
        {
            return WbsValidationResult.Invalid(WbsValidationCode.NoWorkItems);
        }

        if (!MilestoneDeadlineCalculator.TryParseDurationDays(milestoneDuration, out var milestoneDays))
        {
            return WbsValidationResult.Invalid(WbsValidationCode.MilestoneDurationInvalid);
        }

        var totalDays = 0;
        for (var index = 0; index < workItemDurations.Count; index++)
        {
            // TryParseDurationDays already rejects zero and negative amounts.
            if (!MilestoneDeadlineCalculator.TryParseDurationDays(workItemDurations[index], out var days))
            {
                return WbsValidationResult.Invalid(WbsValidationCode.WorkItemDurationInvalid, index);
            }

            totalDays += days;
        }

        if (milestoneDays <= SingleWeekDays)
        {
            if (workItemDurations.Count != 1)
            {
                return WbsValidationResult.Invalid(WbsValidationCode.SingleWeekRequiresExactlyOneItem);
            }

            return totalDays == milestoneDays
                ? WbsValidationResult.Valid()
                : WbsValidationResult.Invalid(WbsValidationCode.SingleWeekItemDurationMismatch, 0);
        }

        return totalDays > milestoneDays
            ? WbsValidationResult.Invalid(WbsValidationCode.TotalDurationExceedsMilestone)
            : WbsValidationResult.Valid();
    }

    /// <summary>
    /// Single source of user-facing copy so every validator, guard and handler reports the same rule the same way.
    /// </summary>
    public static string DescribeError(WbsValidationResult result, string milestoneLabel) => result.Code switch
    {
        WbsValidationCode.NoWorkItems =>
            $"{milestoneLabel} requires at least one work breakdown item.",
        WbsValidationCode.MilestoneDurationInvalid =>
            $"{milestoneLabel} needs a valid estimated duration in weeks, months or years.",
        WbsValidationCode.SingleWeekRequiresExactlyOneItem =>
            $"{milestoneLabel} lasts one week, so it must contain exactly one work breakdown item.",
        WbsValidationCode.SingleWeekItemDurationMismatch =>
            $"The work breakdown item of {milestoneLabel} must last exactly as long as the milestone.",
        WbsValidationCode.WorkItemDurationInvalid =>
            $"Work breakdown item {result.WorkItemIndex + 1} of {milestoneLabel} needs a valid estimated duration in weeks, months or years.",
        WbsValidationCode.TotalDurationExceedsMilestone =>
            $"The work breakdown items of {milestoneLabel} last longer than the milestone itself.",
        _ => string.Empty
    };
}
