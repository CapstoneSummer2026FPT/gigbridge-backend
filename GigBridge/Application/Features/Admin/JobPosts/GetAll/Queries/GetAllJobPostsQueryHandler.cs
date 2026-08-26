using Application.Common.Constants;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Admin.JobPosts.GetAllJobPosts.Queries;

public class GetAllJobPostsQueryHandler : IRequestHandler<GetAllJobPostsQuery, AdminJobPostListResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAllJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminJobPostListResponse> Handle(GetAllJobPostsQuery request, CancellationToken cancellationToken)
    {
        var allJobPosts = _context.Set<JobPost>().AsNoTracking();
        var stats = request.IncludeSummary
            ? await GetStatsAsync(allJobPosts, cancellationToken)
            : null;

        var query = allJobPosts.AsQueryable();
        query = ApplyFilters(query, request);
        var totalItems = ResolveSummaryTotal(request, stats);
        if (!totalItems.HasValue)
        {
            totalItems = await query.CountAsync(cancellationToken);
        }

        query = ApplySorting(query, request);

        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var now = DateTime.UtcNow;
        var rows = await query
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(jobPost => new AdminJobPostListRow(
                jobPost.JobPostsId,
                jobPost.Title,
                jobPost.Description.Length > 200
                    ? jobPost.Description.Substring(0, 200) + "..."
                    : jobPost.Description,
                jobPost.MajorCategoryId,
                jobPost.MajorCategory != null ? jobPost.MajorCategory.MajorId : null,
                jobPost.MajorCategory != null ? jobPost.MajorCategory.Major.Name : null,
                jobPost.MajorCategory != null ? jobPost.MajorCategory.CategoryId : null,
                jobPost.MajorCategory != null ? jobPost.MajorCategory.Category.Name : null,
                jobPost.BudgetMin,
                jobPost.BudgetMax,
                jobPost.CreatedAt,
                jobPost.Status,
                jobPost.Visibility,
                jobPost.ClientProfilesId,
                jobPost.ClientProfiles.User.FullName ?? jobPost.ClientProfiles.CompanyName,
                jobPost.JobPostSkills
                    .Where(link => link.Skills != null)
                    .Select(link => new Application.Features.JobPosts.Common.DTOs.JobPostSkillDto(
                        link.SkillsId,
                        link.Skills.Name))
                    .ToList(),
                jobPost.CustomSkillNames,
                jobPost.IsFeatured && jobPost.FeaturedUntil > now,
                jobPost.FeaturedUntil,
                jobPost.IsAigenerated == true))
            .ToListAsync(cancellationToken);

        var items = rows.Select(MapRow).ToList();

        return new AdminJobPostListResponse(
            items,
            pageIndex,
            pageSize,
            totalItems.Value,
            totalItems.Value == 0 ? 0 : (int)Math.Ceiling(totalItems.Value / (double)pageSize),
            stats);
    }

    private static async Task<AdminJobPostStatsDto> GetStatsAsync(
        IQueryable<JobPost> allJobPosts,
        CancellationToken cancellationToken)
    {
        var stats = await allJobPosts
            .GroupBy(_ => 1)
            .Select(group => new AdminJobPostStatsDto(
                group.Count(),
                group.Count(jobPost => jobPost.Status == 0),
                group.Count(jobPost => jobPost.Status == 1),
                group.Count(jobPost => jobPost.Status == 2),
                group.Count(jobPost => jobPost.Status == 3),
                group.Count(jobPost => jobPost.Visibility == 3)))
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new AdminJobPostStatsDto(0, 0, 0, 0, 0, 0);
    }

    private static int? ResolveSummaryTotal(GetAllJobPostsQuery request, AdminJobPostStatsDto? stats)
    {
        if (stats is null || HasNonStatusFilters(request))
        {
            return null;
        }

        return request.Status switch
        {
            null => stats.Total,
            0 => stats.Draft,
            1 => stats.Open,
            2 => stats.Closed,
            3 => stats.Cancelled,
            _ => 0
        };
    }

    private static bool HasNonStatusFilters(GetAllJobPostsQuery request) =>
        !string.IsNullOrWhiteSpace(request.Search) ||
        request.SkillIds is { Count: > 0 } ||
        request.BudgetMin.HasValue ||
        request.BudgetMax.HasValue;

    private static JobPostSummaryDto MapRow(AdminJobPostListRow row) => new(
        row.JobPostsId,
        row.Title,
        row.DescriptionPreview,
        row.MajorCategoryId,
        row.MajorId,
        row.MajorName,
        row.CategoryId,
        row.CategoryName,
        row.BudgetMin,
        row.BudgetMax,
        row.CreatedAt,
        row.Status,
        row.Visibility,
        UserEloCalculator.DefaultPoints,
        row.ClientProfilesId,
        row.ClientFullName,
        row.Skills,
        row.CustomSkillNames.ToList(),
        row.Skills.Select(skill => skill.SkillName).ToList(),
        row.IsFeatured,
        row.FeaturedUntil,
        row.IsAiGenerated,
        false);

    private static IQueryable<JobPost> ApplyFilters(IQueryable<JobPost> query, GetAllJobPostsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(jobPost =>
                jobPost.Title.ToLower().Contains(keyword) ||
                jobPost.Description.ToLower().Contains(keyword) ||
                (jobPost.MajorCategory != null &&
                    (jobPost.MajorCategory.Major.Name.ToLower().Contains(keyword) ||
                     jobPost.MajorCategory.Category.Name.ToLower().Contains(keyword))) ||
                jobPost.ClientProfiles.User.FullName.ToLower().Contains(keyword) ||
                (jobPost.ClientProfiles.CompanyName != null &&
                    jobPost.ClientProfiles.CompanyName.ToLower().Contains(keyword)) ||
                jobPost.JobPostSkills.Any(jobPostSkill =>
                    jobPostSkill.Skills != null &&
                    jobPostSkill.Skills.Name.ToLower().Contains(keyword)));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(jobPost => jobPost.Status == request.Status.Value);
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
                ? query.OrderByDescending(jobPost => jobPost.BudgetMin).ThenByDescending(jobPost => jobPost.JobPostsId)
                : query.OrderBy(jobPost => jobPost.BudgetMin).ThenBy(jobPost => jobPost.JobPostsId),
            "budgetmax" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.BudgetMax).ThenByDescending(jobPost => jobPost.JobPostsId)
                : query.OrderBy(jobPost => jobPost.BudgetMax).ThenBy(jobPost => jobPost.JobPostsId),
            "title" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.Title).ThenByDescending(jobPost => jobPost.JobPostsId)
                : query.OrderBy(jobPost => jobPost.Title).ThenBy(jobPost => jobPost.JobPostsId),
            "newest" => query.OrderByDescending(jobPost => jobPost.CreatedAt).ThenByDescending(jobPost => jobPost.JobPostsId),
            _ => query.OrderByDescending(jobPost => jobPost.CreatedAt).ThenByDescending(jobPost => jobPost.JobPostsId)
        };
    }

    private static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return Math.Clamp(pageSize, 1, PaginationDefaults.MaxPageSize);
    }

    private sealed record AdminJobPostListRow(
        Guid JobPostsId,
        string Title,
        string DescriptionPreview,
        Guid? MajorCategoryId,
        Guid? MajorId,
        string? MajorName,
        Guid? CategoryId,
        string? CategoryName,
        decimal? BudgetMin,
        decimal? BudgetMax,
        DateTime CreatedAt,
        int Status,
        int? Visibility,
        Guid ClientProfilesId,
        string? ClientFullName,
        List<Application.Features.JobPosts.Common.DTOs.JobPostSkillDto> Skills,
        string[] CustomSkillNames,
        bool IsFeatured,
        DateTime? FeaturedUntil,
        bool IsAiGenerated);
}
