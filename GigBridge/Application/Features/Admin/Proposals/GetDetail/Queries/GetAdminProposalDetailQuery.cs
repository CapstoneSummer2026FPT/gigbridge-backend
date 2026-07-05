using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Proposals.GetDetail.Queries;

public sealed record GetAdminProposalDetailQuery(
    Guid AdminUserId,
    Guid ProposalId) : IRequest<ProposalDto>;

public sealed class GetAdminProposalDetailQueryHandler :
    IRequestHandler<GetAdminProposalDetailQuery, ProposalDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminProposalDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProposalDto> Handle(
        GetAdminProposalDetailQuery request,
        CancellationToken cancellationToken)
    {
        var admin = await _context.Set<User>()
            .FirstOrDefaultAsync(user => user.UserId == request.AdminUserId, cancellationToken);

        if (admin is null || admin.Role != (int)UserRole.Admin)
        {
            throw new ForbiddenAccessException("Only admins can access detailed proposal information.");
        }

        var proposal = await _context.Set<Proposal>()
            .AsNoTracking()
            .Include(proposal => proposal.JobPosts)
            .Include(proposal => proposal.FreelancerProfiles)
                .ThenInclude(freelancerProfile => freelancerProfile.User)
            .FirstOrDefaultAsync(proposal => proposal.ProposalsId == request.ProposalId, cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        var dto = ProposalProjection.ToDtos(new List<Proposal> { proposal }).First();
        return dto;
    }
}
