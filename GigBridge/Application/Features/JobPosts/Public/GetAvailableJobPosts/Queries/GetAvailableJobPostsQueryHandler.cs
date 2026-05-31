using Application.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;

public class GetAvailableJobPostsQueryHandler : IRequestHandler<GetAvailableJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAvailableJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetAvailableJobPostsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Where(jobPost => jobPost.Status == 1 && (jobPost.Visibility == null || jobPost.Visibility == 0));

        query = ApplyFilters(query, request);
        query = ApplySorting(query, request);

        var jobPosts = await query
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        return JobPostProjection.ToSummaryDtos(jobPosts);
    }

    private static IQueryable<JobPost> ApplyFilters(IQueryable<JobPost> query, GetAvailableJobPostsQuery request)
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

    private static IQueryable<JobPost> ApplySorting(IQueryable<JobPost> query, GetAvailableJobPostsQuery request)
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
