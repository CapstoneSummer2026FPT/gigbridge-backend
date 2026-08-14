using Application.Common.Interfaces;
using Application.Features.Seo.PublicMarketplace.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Seo.PublicMarketplace.Queries;

public sealed class GetSeoSitemapResourcesQueryHandler
    : IRequestHandler<GetSeoSitemapResourcesQuery, SeoSitemapResourcesDto>
{
    private readonly IApplicationDbContext _context;

    public GetSeoSitemapResourcesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SeoSitemapResourcesDto> Handle(
        GetSeoSitemapResourcesQuery request,
        CancellationToken cancellationToken)
    {
        var jobs = await _context.Set<Domain.Entities.JobPost>()
            .AsNoTracking()
            .Where(job => job.Status == 1 && (job.Visibility == null || job.Visibility == 0))
            .OrderByDescending(job => job.UpdatedAt ?? job.CreatedAt)
            .Select(job => new SeoSitemapEntryDto(
                job.JobPostsId,
                job.UpdatedAt ?? job.CreatedAt))
            .ToListAsync(cancellationToken);

        var freelancers = await _context.Set<Domain.Entities.FreelancerProfile>()
            .AsNoTracking()
            .Where(profile => profile.AllowSearchEngineIndexing && profile.User.IsActive)
            .OrderByDescending(profile => profile.UpdatedAt ?? profile.CreatedAt)
            .Select(profile => new SeoSitemapEntryDto(
                profile.UserId,
                profile.UpdatedAt ?? profile.CreatedAt))
            .ToListAsync(cancellationToken);

        return new SeoSitemapResourcesDto(jobs, freelancers);
    }
}
