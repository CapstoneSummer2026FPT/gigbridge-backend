using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.MarketplaceAnalytics.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Application.Features.JobPosts.Public.SearchAvailableJobPosts.Commands;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Freelancer.GetProfileMatchedJobPosts.Queries;

public sealed class GetProfileMatchedJobPostsQueryHandler(
    IApplicationDbContext context,
    IDateTimeService clock,
    IMarketplaceAnalyticsRecorder analytics)
    : MediatR.IRequestHandler<GetProfileMatchedJobPostsQuery, PagedJobSearchResponse>
{
    public async Task<PagedJobSearchResponse> Handle(
        GetProfileMatchedJobPostsQuery request,
        CancellationToken cancellationToken)
    {
        var freelancer = await context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (freelancer is null)
        {
            throw new NotFoundException("Freelancer profile not found.");
        }

        var appliedJobIds = context.Set<Proposal>()
            .AsNoTracking()
            .Where(proposal => proposal.FreelancerProfilesId == freelancer.FreelancerProfilesId)
            .Select(proposal => proposal.JobPostsId);

        var jobQuery = ApplyEligibilityFilter(
            context.Set<JobPost>().AsNoTracking(),
            appliedJobIds);

        jobQuery = ApplyMajorMatchFilter(jobQuery, freelancer.MajorId);

        var manualFilters = new SearchAvailableJobPostsCommand(
            PageIndex: request.PageIndex,
            PageSize: request.PageSize,
            Search: request.Search,
            BudgetMin: request.BudgetMin,
            BudgetMax: request.BudgetMax,
            SortBy: request.SortBy,
            SortDesc: request.SortDesc,
            Skills: request.Skills,
            WorkType: request.WorkType,
            PostedWithinDays: request.PostedWithinDays,
            SearchEventId: request.SearchEventId,
            ActorIdentity: $"user:{request.UserId:N}");
        var legacyFilters = new GetAvailableJobPostsQuery(
            request.PageIndex,
            request.PageSize,
            request.Search,
            null,
            request.BudgetMin,
            request.BudgetMax,
            request.SortBy,
            request.SortDesc);
        jobQuery = GetAvailableJobPostsQueryHandler.ApplyFilters(jobQuery, legacyFilters);
        jobQuery = SearchAvailableJobPostsCommandHandler.ApplyBrowseFilters(
            jobQuery,
            manualFilters,
            clock.UtcNow);

        var total = await jobQuery.LongCountAsync(cancellationToken);
        jobQuery = ApplyMajorMatchSorting(jobQuery, request.SortBy, clock.UtcNow);

        var pageIndex = GetAvailableJobPostsQueryHandler.NormalizePageIndex(request.PageIndex);
        var pageSize = GetAvailableJobPostsQueryHandler.NormalizePageSize(request.PageSize);
        var jobs = await jobQuery
            .Include(job => job.ClientProfiles)
                .ThenInclude(client => client.User)
                    .ThenInclude(user => user.UserEloScore)
            .Include(job => job.JobPostSkills)
                .ThenInclude(selection => selection.Skills)
            .Include(job => job.MajorCategory)
                .ThenInclude(mapping => mapping!.Major)
            .Include(job => job.MajorCategory)
                .ThenInclude(mapping => mapping!.Category)
            .AsSplitQuery()
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var jobIds = jobs.Select(job => job.JobPostsId).ToList();
        var aiInterviewIds = jobIds.Count == 0
            ? new HashSet<Guid>()
            : (await context.Set<Domain.Entities.AiInterviewDefinition>()
                .AsNoTracking()
                .Where(definition => jobIds.Contains(definition.JobPostId) &&
                    definition.Status != Domain.Enums.AiInterviews.AiInterviewDefinitionStatus.Closed)
                .Select(definition => definition.JobPostId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var searchEventId = request.SearchEventId;
        if (searchEventId is null && pageIndex == 1)
        {
            try
            {
                searchEventId = await analytics.RecordSearchAsync(
                    $"user:{request.UserId:N}",
                    request.Search,
                    checked((int)Math.Min(total, int.MaxValue)),
                    new
                    {
                        View = "profile",
                        MajorId = freelancer.MajorId,
                        request.BudgetMin,
                        request.BudgetMax,
                        request.Skills,
                        request.WorkType,
                        request.PostedWithinDays,
                        request.SortBy
                    },
                    cancellationToken);
            }
            catch
            {
                searchEventId = null;
            }
        }

        return new PagedJobSearchResponse(
            JobPostProjection.ToSummaryDtos(jobs, clock.UtcNow, aiInterviewIds),
            total,
            pageIndex,
            pageSize,
            searchEventId);
    }

    internal static IQueryable<JobPost> ApplyMajorMatchFilter(
        IQueryable<JobPost> query,
        Guid? majorId)
    {
        if (!majorId.HasValue || majorId.Value == Guid.Empty)
        {
            return query.Where(_ => false);
        }

        return query.Where(job =>
            job.MajorCategory != null && job.MajorCategory.MajorId == majorId.Value);
    }

    internal static IQueryable<JobPost> ApplyEligibilityFilter(
        IQueryable<JobPost> query,
        IQueryable<Guid> appliedJobIds)
    {
        return query.Where(job =>
            job.Status == 1 &&
            (job.Visibility == null || job.Visibility == 0) &&
            !appliedJobIds.Contains(job.JobPostsId));
    }

    internal static IOrderedQueryable<JobPost> ApplyMajorMatchSorting(
        IQueryable<JobPost> query,
        string? sortBy,
        DateTime now)
    {
        return query
            .OrderByDescending(job => job.IsFeatured && job.FeaturedUntil > now)
            .ThenByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.JobPostsId);
    }
}
