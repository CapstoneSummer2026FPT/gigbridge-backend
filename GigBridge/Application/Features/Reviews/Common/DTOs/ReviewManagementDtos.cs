using Domain.Enums.Reviews;

namespace Application.Features.Reviews.Common.DTOs;

public sealed class ManagedReviewDto
{
    public Guid ReviewId { get; init; }
    public Guid ContractId { get; init; }
    public Guid JobPostId { get; init; }
    public string ProjectTitle { get; init; } = string.Empty;
    public Guid ReviewerId { get; init; }
    public string ReviewerName { get; init; } = string.Empty;
    public int ReviewerRole { get; init; }
    public Guid RevieweeId { get; init; }
    public string RevieweeName { get; init; } = string.Empty;
    public int RevieweeRole { get; init; }
    public decimal Rating { get; init; }
    public string? Comment { get; init; }
    public int? CommunicationRating { get; init; }
    public int? QualityRating { get; init; }
    public int? TimelinessRating { get; init; }
    public bool IsAnonymous { get; init; }
    public ReviewModerationStatus ModerationStatus { get; init; }
    public bool HasOpenReport { get; init; }
    public int OpenReportCount { get; init; }
    public int TotalReportCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class MyReviewsResponse
{
    public required IReadOnlyList<ManagedReviewDto> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalItems / (double)PageSize) : 0;
}
