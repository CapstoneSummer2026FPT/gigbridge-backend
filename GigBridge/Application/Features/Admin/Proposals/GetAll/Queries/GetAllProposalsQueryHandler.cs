using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Proposals.GetAllProposals.Queries;

public class GetAllProposalsQueryHandler : IRequestHandler<GetAllProposalsQuery, IEnumerable<ProposalDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAllProposalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProposalDto>> Handle(GetAllProposalsQuery request, CancellationToken cancellationToken)
    {
        var proposals = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.JobPosts)
            .Include(proposal => proposal.FreelancerProfiles)
                .ThenInclude(freelancerProfile => freelancerProfile.User)
            .OrderByDescending(proposal => proposal.SubmittedAt)
            .Skip((NormalizePageIndex(request.PageIndex) - 1) * NormalizePageSize(request.PageSize))
            .Take(NormalizePageSize(request.PageSize))
            .ToListAsync(cancellationToken);

        return ProposalProjection.ToDtos(proposals);
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
