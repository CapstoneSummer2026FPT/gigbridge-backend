using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Client.GetProposalsByJobPost.Queries;

public class GetProposalsByJobPostQueryHandler : IRequestHandler<GetProposalsByJobPostQuery, IEnumerable<ProposalDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProposalsByJobPostQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ProposalDto>> Handle(GetProposalsByJobPostQuery request, CancellationToken cancellationToken)
    {
        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null)
        {
            throw new NotFoundException("Client profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(jobPost => jobPost.JobPostsId == request.JobPostsId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not have permission to view proposals for this job.");
        }

        var proposals = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.JobPosts)
            .Include(proposal => proposal.FreelancerProfiles)
                .ThenInclude(freelancerProfile => freelancerProfile.User)
            .Where(proposal => proposal.JobPostsId == request.JobPostsId && proposal.Status != 0)
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
