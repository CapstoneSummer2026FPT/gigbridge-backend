using Application.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

<<<<<<<< HEAD:GigBridge/Application/Features/Admin/Jobposts/GetAllJobPosts/Queries/GetAllJobPostsQueryHandler.cs
namespace Application.Features.Admin.Jobpost.GetAllJobPosts.Queries;
========
namespace Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;
>>>>>>>> 2399a82a7a1aab05aec2a83005037a29808e8e3f:GigBridge/Application/Features/Admin/JobPosts/GetAll/Queries/GetAllJobPostsQueryHandler.cs

public class GetAllJobPostsQueryHandler : IRequestHandler<GetAllJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetAllJobPostsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .AsQueryable();

        query = ApplyFilters(query, request);
        query = ApplySorting(query, request);

        var jobPosts = await query
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        return JobPostProjection.ToSummaryDtos(jobPosts);
    }

    private static IQueryable<JobPost> ApplyFilters(IQueryable<JobPost> query, GetAllJobPostsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(jobPost =>
                jobPost.Title.ToLower().Contains(keyword) ||
                jobPost.Description.ToLower().Contains(keyword) ||
                jobPost.JobPostSkills.Any(jobPostSkill =>
                    jobPostSkill.Skills != null &&
                    jobPostSkill.Skills.Name.ToLower().Contains(keyword)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(jobPost => jobPost.Status == request.Status.Value);
        }

        if (request.BudgetType.HasValue)
        {
            query = query.Where(jobPost => jobPost.BudgetType == request.BudgetType.Value);
        }

        if (request.SkillIds is { Count: > 0 })
        {
            query = query.Where(jobPost =>
                jobPost.JobPostSkills.Any(jobPostSkill => request.SkillIds.Contains(jobPostSkill.SkillsId)));
        }

        if (request.BudgetMin.HasValue)
        {
            query = query.Where(jobPost => !jobPost.BudgetMax.HasValue || jobPost.BudgetMax >= request.BudgetMin.Value);
        }

        if (request.BudgetMax.HasValue)
        {
            query = query.Where(jobPost => !jobPost.BudgetMin.HasValue || jobPost.BudgetMin <= request.BudgetMax.Value);
        }

        return query;
    }

    private static IQueryable<JobPost> ApplySorting(IQueryable<JobPost> query, GetAllJobPostsQuery request)
    {
        return request.SortBy?.Trim().ToLowerInvariant() switch
        {
            "budgetmin" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.BudgetMin)
                : query.OrderBy(jobPost => jobPost.BudgetMin),
            "budgetmax" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.BudgetMax)
                : query.OrderBy(jobPost => jobPost.BudgetMax),
            "newest" => query.OrderByDescending(jobPost => jobPost.CreatedAt),
            _ => query.OrderByDescending(jobPost => jobPost.CreatedAt)
        };
    }

    private static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize is < 1 or > 100 ? 10 : pageSize;
    }
}
