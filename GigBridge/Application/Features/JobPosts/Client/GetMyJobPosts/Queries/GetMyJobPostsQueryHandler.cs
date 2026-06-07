using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobPosts.Common;
using Application.Features.JobPosts.Public.GetAvailableJobPosts.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GetMyJobPosts.Queries;

public class GetMyJobPostsQueryHandler : IRequestHandler<GetMyJobPostsQuery, IEnumerable<JobPostSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyJobPostsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobPostSummaryDto>> Handle(GetMyJobPostsQuery request, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPosts = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(jobPost => jobPost.JobPostSkills)
                .ThenInclude(jobPostSkill => jobPostSkill.Skills)
            .Where(jobPost => jobPost.ClientProfilesId == clientProfile.ClientProfilesId)
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
