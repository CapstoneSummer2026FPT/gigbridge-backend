using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using MediatR;

namespace Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;

public record GetAllJobPostsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    string? Search = null,
    int? Status = null,
    List<Guid>? SkillIds = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? SortBy = null,
    bool SortDesc = true
) : IRequest<IEnumerable<JobPostSummaryDto>>;
