using Application.Common.InternalServices.Scheduling;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices.Scheduling;

/// <summary>
/// Pins the work breakdown rules the product asked for: a one-week milestone holds exactly one work item,
/// a longer milestone may be split as long as the parts never outlast the whole, and work item deadlines
/// chain inside the milestone the same way milestones chain inside the project.
/// </summary>
public class WorkBreakdownScheduleCalculatorTests
{
    [Fact]
    public void Validate_RejectsMilestoneWithoutWorkItems()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("2 weeks", []);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.NoWorkItems, result.Code);
    }

    [Fact]
    public void Validate_OneWeekMilestoneAcceptsExactlyOneMatchingWorkItem()
    {
        Assert.True(WorkBreakdownScheduleCalculator.Validate("1 week", ["1 week"]).IsValid);
    }

    [Fact]
    public void Validate_OneWeekMilestoneRejectsASecondWorkItem()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("1 week", ["1 week", "1 week"]);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.SingleWeekRequiresExactlyOneItem, result.Code);
    }

    [Fact]
    public void Validate_OneWeekMilestoneRequiresItsSingleItemToSpanTheWholeMilestone()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("1 week", ["2 weeks"]);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.SingleWeekItemDurationMismatch, result.Code);
    }

    [Fact]
    public void Validate_LongerMilestoneAllowsSeveralWorkItemsUpToItsOwnDuration()
    {
        Assert.True(WorkBreakdownScheduleCalculator.Validate("2 weeks", ["1 week", "1 week"]).IsValid);
        Assert.True(WorkBreakdownScheduleCalculator.Validate("1 month", ["1 week", "2 weeks"]).IsValid);
    }

    [Fact]
    public void Validate_RejectsWorkItemsThatOutlastTheirMilestone()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("2 weeks", ["1 week", "1 week", "1 week"]);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.TotalDurationExceedsMilestone, result.Code);
    }

    [Fact]
    public void Validate_ReportsTheIndexOfAnUnparseableWorkItemDuration()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("1 month", ["1 week", "5 days"]);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.WorkItemDurationInvalid, result.Code);
        Assert.Equal(1, result.WorkItemIndex);
    }

    [Fact]
    public void Validate_RejectsAMilestoneWhoseOwnDurationCannotBeParsed()
    {
        var result = WorkBreakdownScheduleCalculator.Validate("soon", ["1 week"]);

        Assert.False(result.IsValid);
        Assert.Equal(WbsValidationCode.MilestoneDurationInvalid, result.Code);
    }

    [Fact]
    public void CalculateWorkItemDueDates_ChainsInsideTheMilestoneAndEndsOnItsDeadline()
    {
        // Milestone 1 of a project anchored at Aug 1 runs Aug 2 - Aug 15 (2 weeks).
        var anchor = new DateOnly(2026, 8, 1);

        var dueDates = WorkBreakdownScheduleCalculator.CalculateWorkItemDueDates(anchor, ["1 week", "1 week"]);

        Assert.Equal(new DateOnly(2026, 8, 8), dueDates[0]);
        Assert.Equal(new DateOnly(2026, 8, 15), dueDates[1]);
    }

    [Fact]
    public void CalculateWorkItemDueDates_StopsSchedulingAfterAnUnparseableDuration()
    {
        var dueDates = WorkBreakdownScheduleCalculator.CalculateWorkItemDueDates(
            new DateOnly(2026, 8, 1),
            ["1 week", "whenever", "1 week"]);

        Assert.Equal(new DateOnly(2026, 8, 8), dueDates[0]);
        Assert.Null(dueDates[1]);
        Assert.Null(dueDates[2]);
    }

    [Fact]
    public void ResolveMilestoneStartAnchor_UsesThePlanAnchorFirstThenThePreviousMilestoneDeadline()
    {
        var planAnchor = new DateOnly(2026, 8, 1);
        var milestoneDueDates = new List<DateOnly?>
        {
            new DateOnly(2026, 8, 15),
            new DateOnly(2026, 8, 29)
        };

        Assert.Equal(planAnchor, WorkBreakdownScheduleCalculator.ResolveMilestoneStartAnchor(planAnchor, milestoneDueDates, 0));
        Assert.Equal(new DateOnly(2026, 8, 15), WorkBreakdownScheduleCalculator.ResolveMilestoneStartAnchor(planAnchor, milestoneDueDates, 1));
        Assert.Equal(new DateOnly(2026, 8, 29), WorkBreakdownScheduleCalculator.ResolveMilestoneStartAnchor(planAnchor, milestoneDueDates, 2));
    }

    [Fact]
    public void ResolveMilestoneStartAnchor_ReturnsNullWhenTheMilestoneIsOutsideTheComputedChain()
    {
        Assert.Null(WorkBreakdownScheduleCalculator.ResolveMilestoneStartAnchor(
            new DateOnly(2026, 8, 1),
            [new DateOnly(2026, 8, 15)],
            5));
    }

    [Fact]
    public void ResolveMilestoneStartAnchor_PropagatesAnUnscheduledPreviousMilestone()
    {
        var milestoneDueDates = new List<DateOnly?> { null };

        Assert.Null(WorkBreakdownScheduleCalculator.ResolveMilestoneStartAnchor(
            new DateOnly(2026, 8, 1),
            milestoneDueDates,
            1));
    }

    [Fact]
    public void DescribeError_NamesTheOffendingWorkItemUsingAOneBasedPosition()
    {
        var result = WbsValidationResult.Invalid(WbsValidationCode.WorkItemDurationInvalid, 1);

        Assert.Contains("Work breakdown item 2", WorkBreakdownScheduleCalculator.DescribeError(result, "Milestone 1"));
    }
}
