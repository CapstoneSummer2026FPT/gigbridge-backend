using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.GetMyProposalByJobPost.Queries;

public class GetMyProposalByJobPostQueryHandler
    : IRequestHandler<GetMyProposalByJobPostQuery, ProposalDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyProposalByJobPostQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProposalDetailDto> Handle(
        GetMyProposalByJobPostQuery request,
        CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.UserId == request.UserId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var proposal = await _context.Set<Proposal>()
            .Include(proposal => proposal.JobPosts)
            .Include(proposal => proposal.FreelancerProfiles)
                .ThenInclude(freelancerProfile => freelancerProfile.User)
            .FirstOrDefaultAsync(
                proposal =>
                    proposal.JobPostsId == request.JobPostId &&
                    proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
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