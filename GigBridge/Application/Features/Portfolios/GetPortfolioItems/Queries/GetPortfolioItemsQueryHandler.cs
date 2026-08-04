using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Portfolios.Common;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Portfolios.GetPortfolioItems.Queries;

public sealed class GetPortfolioItemsQueryHandler
    : IRequestHandler<GetPortfolioItemsQuery, IReadOnlyList<PortfolioItemDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPortfolioItemsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PortfolioItemDto>> Handle(
        GetPortfolioItemsQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            throw new NotFoundException(nameof(FreelancerProfile), request.UserId);
        }

        var items = await _context.Set<PortfolioItem>()
            .AsNoTracking()
            .Where(item => item.FreelancerId == profile.FreelancerProfilesId)
            .OrderByDescending(item => item.ProjectDate)
            .ThenBy(item => item.PortfolioItemsId)
            .ToListAsync(cancellationToken);

        return items.Select(item => item.ToDto()).ToList();
    }
}
