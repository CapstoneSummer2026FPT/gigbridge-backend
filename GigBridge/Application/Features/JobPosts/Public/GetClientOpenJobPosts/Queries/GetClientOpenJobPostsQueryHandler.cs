using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.Queries;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Public.GetClientOpenJobPosts.Queries;

public class GetClientOpenJobPostsQueryHandler : IRequestHandler<GetClientOpenJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _clock;

    public GetClientOpenJobPostsQueryHandler(IApplicationDbContext context, IDateTimeService clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetClientOpenJobPostsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = GetAvailableJobPostsQueryHandler.NormalizePageIndex(request.PageIndex);
        var pageSize = GetAvailableJobPostsQueryHandler.NormalizePageSize(request.PageSize);
        var now = _clock.UtcNow;

        var jobPosts = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile!.User)
                .ThenInclude(user => user!.UserEloScore)
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Major)
            .Include(jobPost => jobPost.MajorCategory)
                .ThenInclude(majorCategory => majorCategory!.Category)
            .Where(jobPost =>
                jobPost.ClientProfiles.UserId == request.ClientUserId &&
                jobPost.Status == 1 &&
                (jobPost.Visibility == null || jobPost.Visibility == 0))
            .OrderByDescending(jobPost => jobPost.IsFeatured && jobPost.FeaturedUntil > now)
            .ThenByDescending(jobPost => jobPost.ClientProfiles.User.UserEloScore != null
                ? jobPost.ClientProfiles.User.UserEloScore.CurrentPoints
                : UserEloCalculator.DefaultPoints)
            .ThenByDescending(jobPost => jobPost.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
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

        return JobPostProjection.ToSummaryDtos(jobPosts, now, aiInterviewJobIds);
    }
}
