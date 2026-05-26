namespace Application.Features.Admin.Dashboard.Dto;

public sealed class DashboardSummaryDto
{
    public int OpenJobs { get; init; }
    public int PendingReports { get; init; }
    public int OpenDisputes { get; init; }
    public int HiddenReviews { get; init; }
}
