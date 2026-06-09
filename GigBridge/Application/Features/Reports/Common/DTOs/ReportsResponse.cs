namespace Application.Features.Reports.Common.DTOs;

public class ReportsResponse
{
    public required IReadOnlyList<ReportDto> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
}
