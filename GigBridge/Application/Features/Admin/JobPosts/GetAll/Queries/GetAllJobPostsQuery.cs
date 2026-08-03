using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using MediatR;

namespace Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;

public sealed record AdminJobPostStatsDto(
    int Total,
    int Draft,
    int Open,
    int Closed,
    int Cancelled,
    int Locked);

public sealed record AdminJobPostListResponse(
    IReadOnlyList<JobPostSummaryDto> Items,
    int PageIndex,
    int PageSize,
    int TotalItems,
    int TotalPages,
    AdminJobPostStatsDto? Stats);

public record GetAllJobPostsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    string? Search = null,
    int? Status = null,
    List<Guid>? SkillIds = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? SortBy = null,
    bool SortDesc = true,
    bool IncludeSummary = true,
    int? KnownTotalItems = null
) : IRequest<AdminJobPostListResponse>;
