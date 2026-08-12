using Domain.Enums.AiInterviews;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Application.Common.Interfaces.Time;
using Domain.Entities;

using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;

public class GetAvailableJobPostsQueryHandler : IRequestHandler<GetAvailableJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public GetAvailableJobPostsQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetAvailableJobPostsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile.User)
                .ThenInclude(user => user.UserEloScore)
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Major)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Category)
            .Where(jobPost => jobPost.Status == 1 && (jobPost.Visibility == null || jobPost.Visibility == 0));

        query = ApplyFilters(query, request);
        query = ApplySorting(query, request, _clock.UtcNow);

        var jobPosts = await query
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        var jobPostIds = jobPosts.Select(jobPost => jobPost.JobPostsId).ToList();
        var aiInterviewJobIds = jobPostIds.Count == 0
            ? new HashSet<Guid>()
            : (await _context.Set<AiInterviewDefinition>()
                .AsNoTracking()
                .Where(definition => jobPostIds.Contains(definition.JobPostId) &&
                    definition.Status != AiInterviewDefinitionStatus.Closed)
                .Select(definition => definition.JobPostId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        return JobPostProjection.ToSummaryDtos(jobPosts, _clock.UtcNow, aiInterviewJobIds);
    }

    internal static IQueryable<JobPost> ApplyFilters(IQueryable<JobPost> query, GetAvailableJobPostsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLower();
            query = query.Where(jobPost =>
                jobPost.Title.ToLower().Contains(keyword) ||
                jobPost.Description.ToLower().Contains(keyword) ||
                jobPost.JobPostSkills.Any(jobPostSkill =>
                    jobPostSkill.Skills != null &&
                    jobPostSkill.Skills.Name.ToLower().Contains(keyword)) ||
                jobPost.CustomSkillNames.Any(skillName =>
                    skillName.ToLower().Contains(keyword)));
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

    internal static IQueryable<JobPost> ApplySorting(
        IQueryable<JobPost> query,
        GetAvailableJobPostsQuery request,
        DateTime now)
    {
        return request.SortBy?.Trim().ToLowerInvariant() switch
        {
            "budgetmin" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                    .ThenByDescending(jobPost => jobPost.BudgetMin)
                : query.OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                    .ThenBy(jobPost => jobPost.BudgetMin),
            "budgetmax" => request.SortDesc
                ? query.OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                    .ThenByDescending(jobPost => jobPost.BudgetMax)
                : query.OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                    .ThenBy(jobPost => jobPost.BudgetMax),
            "newest" => query
                .OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                .ThenByDescending(jobPost => jobPost.ClientProfiles.User.UserEloScore != null
                    ? jobPost.ClientProfiles.User.UserEloScore.CurrentPoints
                    : UserEloCalculator.DefaultPoints)
                .ThenByDescending(jobPost => jobPost.CreatedAt),
            _ => query
                .OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
                .ThenByDescending(jobPost => jobPost.ClientProfiles.User.UserEloScore != null
                    ? jobPost.ClientProfiles.User.UserEloScore.CurrentPoints
                    : UserEloCalculator.DefaultPoints)
                .ThenByDescending(jobPost => jobPost.CreatedAt)
        };
    }

    internal static int NormalizePageIndex(int pageIndex)
    {
        return pageIndex < 1 ? 1 : pageIndex;
    }

    internal static int NormalizePageSize(int pageSize)
    {
        return pageSize is < 1 or > 100 ? 10 : pageSize;
    }
}
