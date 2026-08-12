using Domain.Enums.AiInterviews;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Application.Features.MarketplaceAnalytics.Common.Services;
using Domain.Entities;

using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;

public sealed class SearchAvailableJobPostsCommandHandler
    : MediatR.IRequestHandler<SearchAvailableJobPostsCommand, PagedJobSearchResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;
    private readonly IMarketplaceAnalyticsRecorder _analytics;

    public SearchAvailableJobPostsCommandHandler(
        IApplicationDbContext context,
        IDateTimeService clock,
        IMarketplaceAnalyticsRecorder analytics)
    {
        _context = context;
        _clock = clock;
        _analytics = analytics;
    }

    public async Task<PagedJobSearchResponse> Handle(SearchAvailableJobPostsCommand request, CancellationToken cancellationToken)
    {
        var legacy = new GetAvailableJobPostsQuery(request.PageIndex, request.PageSize, request.Search,
            request.SkillIds, request.BudgetMin, request.BudgetMax, request.SortBy, request.SortDesc);
        var query = _context.Set<JobPost>().AsNoTracking()
            .Where(x => x.Status == 1 && (x.Visibility == null || x.Visibility == 0));
        query = GetAvailableJobPostsQueryHandler.ApplyFilters(query, legacy);
        query = ApplyBrowseFilters(query, request, _clock.UtcNow);
        var total = await query.LongCountAsync(cancellationToken);
        query = ApplyBrowseSorting(query, request, _clock.UtcNow);
        var pageIndex = GetAvailableJobPostsQueryHandler.NormalizePageIndex(request.PageIndex);
        var pageSize = GetAvailableJobPostsQueryHandler.NormalizePageSize(request.PageSize);
        var jobs = await query
            .Include(x => x.ClientProfiles).ThenInclude(x => x.User).ThenInclude(x => x.UserEloScore)
            .Include(x => x.JobPostSkills).ThenInclude(x => x.Skills)
            .Include(x => x.MajorCategory).ThenInclude(x => x!.Major)
            .Include(x => x.MajorCategory).ThenInclude(x => x!.Category)
            .AsSplitQuery()
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ids = jobs.Select(x => x.JobPostsId).ToList();
        var aiInterviewIds = ids.Count == 0 ? new HashSet<Guid>() :
            (await _context.Set<AiInterviewDefinition>().AsNoTracking()
                .Where(x => ids.Contains(x.JobPostId) && x.Status != AiInterviewDefinitionStatus.Closed)
                .Select(x => x.JobPostId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        var eventId = request.SearchEventId;
        if (eventId is null && pageIndex == 1)
        {
            try
            {
                eventId = await _analytics.RecordSearchAsync(
                    request.ActorIdentity,
                    request.Search,
                    checked((int)Math.Min(total, int.MaxValue)),
                    new
                    {
                        request.SkillIds, request.BudgetMin, request.BudgetMax, request.SortBy, request.SortDesc,
                        request.Category, request.Skills, request.WorkType, request.PostedWithinDays, request.AiOnly
                    },
                    cancellationToken);
            }
            catch
            {
                // Telemetry is non-critical and must not interrupt job discovery.
                eventId = null;
            }
        }

        return new PagedJobSearchResponse(JobPostProjection.ToSummaryDtos(jobs, _clock.UtcNow, aiInterviewIds),
            total, pageIndex, pageSize, eventId);
    }

    internal static IQueryable<JobPost> ApplyBrowseFilters(
        IQueryable<JobPost> query,
        SearchAvailableJobPostsCommand request,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(request.Category) &&
            !request.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var category = request.Category.Trim();
            query = query.Where(job => job.MajorCategory != null &&
                job.MajorCategory.Category != null && job.MajorCategory.Category.Name == category);
        }

        var skillTerms = (request.Skills ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .Distinct()
            .Take(10)
            .ToArray();
        foreach (var term in skillTerms)
        {
            query = query.Where(job =>
                job.JobPostSkills.Any(link => link.Skills != null &&
                    link.Skills.Name.ToLower().Contains(term)) ||
                job.CustomSkillNames.Any(name => name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.WorkType) &&
            !request.WorkType.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            !request.WorkType.Equals("fixed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(_ => false);
        }

        if (request.PostedWithinDays is > 0)
        {
            var cutoff = now.AddDays(-Math.Min(request.PostedWithinDays.Value, 3650));
            query = query.Where(job => job.CreatedAt >= cutoff);
        }

        if (request.AiOnly)
            query = query.Where(job => job.IsAigenerated == true);

        return query;
    }

    internal static IQueryable<JobPost> ApplyBrowseSorting(
        IQueryable<JobPost> query,
        SearchAvailableJobPostsCommand request,
        DateTime now)
    {
        if (request.SortBy?.Trim().Equals("newest", StringComparison.OrdinalIgnoreCase) == true)
        {
            return query.OrderByDescending(job => job.IsFeatured && job.FeaturedUntil > now)
                .ThenByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.JobPostsId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim().ToLowerInvariant();
            return query.OrderByDescending(job => job.IsFeatured && job.FeaturedUntil > now)
                .ThenByDescending(job => job.Title.ToLower().Contains(keyword))
                .ThenByDescending(job => job.JobPostSkills.Any(link => link.Skills != null &&
                    link.Skills.Name.ToLower().Contains(keyword)))
                .ThenByDescending(job => job.CustomSkillNames.Any(name => name.ToLower().Contains(keyword)))
                .ThenByDescending(job => job.Description.ToLower().Contains(keyword))
                .ThenByDescending(job => job.ClientProfiles.User.UserEloScore != null
                    ? job.ClientProfiles.User.UserEloScore.CurrentPoints
                    : UserEloCalculator.DefaultPoints)
                .ThenByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.JobPostsId);
        }

        return query.OrderByDescending(job => job.IsFeatured && job.FeaturedUntil > now)
            .ThenByDescending(job => job.ClientProfiles.User.UserEloScore != null
                ? job.ClientProfiles.User.UserEloScore.CurrentPoints
                : UserEloCalculator.DefaultPoints)
            .ThenByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.JobPostsId);
    }
}
