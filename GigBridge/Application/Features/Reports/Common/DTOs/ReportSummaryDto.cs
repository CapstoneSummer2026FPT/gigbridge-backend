namespace Application.Features.Reports.Common.DTOs;

public class ReportSummaryDto
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Reviewing { get; init; }
    public int Resolved { get; init; }
    public int Dismissed { get; init; }
    public int Open { get; init; }
}
