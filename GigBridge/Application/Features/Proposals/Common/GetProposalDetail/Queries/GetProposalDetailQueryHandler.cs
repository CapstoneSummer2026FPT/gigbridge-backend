using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.GetProposalDetail.Queries;

public class GetProposalDetailQueryHandler
    : IRequestHandler<GetProposalDetailQuery, ProposalDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetProposalDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProposalDetailDto> Handle(
        GetProposalDetailQuery request,
        CancellationToken cancellationToken)
    {
        var proposal = await _context.Set<Proposal>()
            .Include(p => p.JobPosts)
            .Include(p => p.FreelancerProfiles)
                .ThenInclude(fp => fp.User)
            .FirstOrDefaultAsync(
                p => p.ProposalsId == request.ProposalId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        var isClientOwner = await _context.Set<ClientProfile>()
            .AnyAsync(
                clientProfile =>
                    clientProfile.UserId == request.UserId &&
                    clientProfile.ClientProfilesId == proposal.JobPosts.ClientProfilesId,
                cancellationToken);

        var isFreelancerOwner = await _context.Set<FreelancerProfile>()
            .AnyAsync(
                freelancerProfile =>
                    freelancerProfile.UserId == request.UserId &&
                    freelancerProfile.FreelancerProfilesId == proposal.FreelancerProfilesId,
                cancellationToken);

        if (!isClientOwner && !isFreelancerOwner)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this proposal.");
        }

        return new ProposalDetailDto
        {
            ProposalId = proposal.ProposalsId,
            JobPostId = proposal.JobPostsId,
            JobPostTitle = proposal.JobPosts.Title,
            FreelancerProfileId = proposal.FreelancerProfilesId,
            FreelancerName = proposal.FreelancerProfiles.User.FullName,
            CoverLetter = proposal.CoverLetter,
            ProposedRate = proposal.ProposedRate,
            ProposedDuration = proposal.ProposedDuration,
            Status = proposal.Status,
            SubmittedAt = proposal.SubmittedAt,
            UpdatedAt = proposal.UpdatedAt,
            IsAigenerated = proposal.IsAigenerated
        };
    }
}