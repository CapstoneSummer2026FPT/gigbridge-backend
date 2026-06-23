using Application.Common.Interfaces;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobInvitations.Freelancer.GetMyInvitations.Queries;

public sealed class GetMyJobInvitationsQueryHandler
    : IRequestHandler<GetMyJobInvitationsQuery, IEnumerable<JobInvitationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyJobInvitationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobInvitationDto>> Handle(
        GetMyJobInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await JobInvitationRules.GetFreelancerProfileAsync(
            _context,
            request.UserId,
            cancellationToken);

        var query = _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(invitation => invitation.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId);

        if (request.Status.HasValue)
        {
            query = query.Where(invitation => invitation.Status == request.Status.Value);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 10 : request.PageSize;

        return await query
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectToJobInvitationDto()
            .ToListAsync(cancellationToken);
    }
}
