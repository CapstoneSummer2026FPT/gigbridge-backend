using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Disputes.Common.DTOs;
using Application.Features.Disputes.Common.Internal;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disputes.Common.GetMyDisputes.Queries;

public sealed class GetMyDisputesQueryHandler : IRequestHandler<GetMyDisputesQuery, MyDisputesResponse>
{
    private readonly IApplicationDbContext _context;

    public GetMyDisputesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MyDisputesResponse> Handle(GetMyDisputesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, PaginatedQuery.MaxPageSize);

        var clientProfile = await _context.Set<ClientProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (clientProfile is null && freelancerProfile is null)
        {
            return new MyDisputesResponse([], page, pageSize, 0);
        }

        // Security boundary: a dispute is only returned if the requesting user is the
        // Client or Freelancer on its contract — resolved from the JWT-authenticated
        // UserId, never from anything supplied by the frontend.
        var query = _context.Set<Dispute>()
            .AsNoTracking()
            .Include(dispute => dispute.Contracts).ThenInclude(contract => contract.JobPosts)
            .Include(dispute => dispute.Contracts).ThenInclude(contract => contract.Milestones)
            .Include(dispute => dispute.Milestones)
            .Where(dispute =>
                (clientProfile != null && dispute.Contracts.ClientProfilesId == clientProfile.ClientProfilesId) ||
                (freelancerProfile != null && dispute.Contracts.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId));

        var total = await query.CountAsync(cancellationToken);
        var disputes = await query
            .OrderByDescending(dispute => dispute.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = disputes.Select(dispute => new MyDisputeSummaryResponse(
            dispute.DisputesId,
            dispute.ContractsId,
            dispute.Contracts.JobPostsId,
            dispute.Contracts.JobPosts.Title,
            dispute.CreatedAt,
            dispute.Status,
            dispute.MilestonesId,
            dispute.Milestones?.Title,
            dispute.Status == (int)Domain.Enums.Disputes.DisputeStatus.Resolved &&
                DisputeJobPostRecreationSupport.IsEligible(dispute.Contracts.Status, dispute.Contracts.Milestones))).ToList();

        return new MyDisputesResponse(items, page, pageSize, total);
    }
}
