using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using MediatR;

<<<<<<<< HEAD:GigBridge/Application/Features/Admin/Jobposts/GetAllJobPosts/Queries/GetAllJobPostsQuery.cs
namespace Application.Features.Admin.Jobpost.GetAllJobPosts.Queries;
========
namespace Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;
>>>>>>>> 2399a82a7a1aab05aec2a83005037a29808e8e3f:GigBridge/Application/Features/Admin/JobPosts/GetAll/Queries/GetAllJobPostsQuery.cs

public record GetAllJobPostsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    string? Search = null,
    int? Status = null,
    int? BudgetType = null,
    List<Guid>? SkillIds = null,
    decimal? BudgetMin = null,
    decimal? BudgetMax = null,
    string? SortBy = null,
    bool SortDesc = true
) : IRequest<IEnumerable<JobPostSummaryDto>>;
