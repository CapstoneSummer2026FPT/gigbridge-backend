using Application.Common.Interfaces;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobInvitations.Client.GetMySentInvitations.Queries;

public sealed class GetMySentJobInvitationsQueryHandler
    : IRequestHandler<GetMySentJobInvitationsQuery, IEnumerable<JobInvitationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMySentJobInvitationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobInvitationDto>> Handle(
        GetMySentJobInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            request.UserId,
            cancellationToken);

        var query = _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(invitation => invitation.ClientProfilesId == clientProfile.ClientProfilesId);

        if (request.Status.HasValue)
        {
            query = query.Where(invitation => invitation.Status == request.Status.Value);
        }

        if (request.JobPostId.HasValue)
        {
            query = query.Where(invitation => invitation.JobPostsId == request.JobPostId.Value);
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
