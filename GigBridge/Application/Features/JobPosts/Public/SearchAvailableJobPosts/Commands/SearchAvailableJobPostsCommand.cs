using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using MediatR;

namespace Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;

public sealed record SearchAvailableJobPostsCommand(
    int PageIndex = 1,
    int PageSize = 12,
    string? Search = null,
    List<Guid>? SkillIds = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? SortBy = null,
    bool SortDesc = true,
    string? Category = null,
    string? Skills = null,
    string? WorkType = null,
    int? PostedWithinDays = null,
    bool AiOnly = false,
    Guid? SearchEventId = null,
    string ActorIdentity = "") : IRequest<PagedJobSearchResponse>;

public sealed record PagedJobSearchResponse(
    IReadOnlyList<JobPostSummaryDto> Items,
    long TotalResults,
    int PageIndex,
    int PageSize,
    Guid? SearchEventId);
