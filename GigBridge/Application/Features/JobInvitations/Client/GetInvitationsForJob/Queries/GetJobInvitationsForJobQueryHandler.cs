using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.JobInvitations.Common;
using Application.Features.JobInvitations.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobInvitations.Client.GetInvitationsForJob.Queries;

public sealed class GetJobInvitationsForJobQueryHandler
    : IRequestHandler<GetJobInvitationsForJobQuery, IEnumerable<JobInvitationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetJobInvitationsForJobQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<JobInvitationDto>> Handle(
        GetJobInvitationsForJobQuery request,
        CancellationToken cancellationToken)
    {
        var clientProfile = await JobInvitationRules.GetClientProfileAsync(
            _context,
            request.UserId,
            cancellationToken);

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .FirstOrDefaultAsync(job => job.JobPostsId == request.JobPostId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        if (jobPost.ClientProfilesId != clientProfile.ClientProfilesId)
        {
            throw new ForbiddenAccessException("You do not own this job post.");
        }

        return await _context.Set<JobInvitation>()
            .AsNoTracking()
            .Where(invitation => invitation.JobPostsId == request.JobPostId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ProjectToJobInvitationDto()
            .ToListAsync(cancellationToken);
    }
}
