using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
using MediatR;

namespace Application.Features.JobPosts.Freelancer.GetProfileMatchedJobPosts.Queries;

public sealed record GetProfileMatchedJobPostsQuery(
    Guid UserId,
    int PageIndex,
    int PageSize,
    IReadOnlyList<Guid> MajorCategoryIds,
    string? Search,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Skills,
    string? WorkType,
    int? PostedWithinDays,
    string? SortBy,
    bool SortDesc,
    Guid? SearchEventId) : IRequest<PagedJobSearchResponse>;
