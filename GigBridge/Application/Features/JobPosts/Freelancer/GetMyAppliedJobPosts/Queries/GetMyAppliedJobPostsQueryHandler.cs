using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Freelancer.GetMyAppliedJobPosts.Queries;

public class GetMyAppliedJobPostsQueryHandler : IRequestHandler<GetMyAppliedJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyAppliedJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetMyAppliedJobPostsQuery request, CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var jobPosts = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Where(jobPost => jobPost.Proposals.Any(proposal =>
                proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId))
            .OrderByDescending(jobPost => jobPost.CreatedAt)
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        return JobPostProjection.ToSummaryDtos(jobPosts);
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
