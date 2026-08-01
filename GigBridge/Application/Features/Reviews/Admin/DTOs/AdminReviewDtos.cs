using Application.Features.Reviews.Common.DTOs;

namespace Application.Features.Reviews.Admin.DTOs;

public sealed class AdminReviewSummaryDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int Hidden { get; init; }
    public int WithOpenReports { get; init; }
}

public sealed class AdminReviewsResponse
{
    public required IReadOnlyList<ManagedReviewDto> Items { get; init; }
    public required AdminReviewSummaryDto Summary { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
}
