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
            .Include(p => p.ProposalWorkBreakdownItems)
            .Include(p => p.ProposalMilestonePlans)
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
            FreelancerUserId = proposal.FreelancerProfiles.User?.UserId,
            CoverLetter = proposal.CoverLetter,
            ProposedBudget = proposal.ProposedBudget,
            ProposedDuration = proposal.ProposedDuration,
            AnalysisSummary = proposal.AnalysisSummary,
            SolutionApproach = proposal.SolutionApproach,
            Deliverables = proposal.Deliverables,
            Assumptions = proposal.Assumptions,
            OutOfScope = proposal.OutOfScope,
            WorkBreakdownItems = proposal.ProposalWorkBreakdownItems
                .OrderBy(item => item.OrderIndex)
                .Select(ProposalPlanMapper.ToDto)
                .ToList(),
            MilestonePlans = proposal.ProposalMilestonePlans
                .OrderBy(item => item.OrderIndex)
                .Select(ProposalPlanMapper.ToDto)
                .ToList(),
            Status = proposal.Status,
            ModerationStatus = proposal.ModerationStatus,
            InvalidationReason = proposal.InvalidationReason,
            SubmittedAt = proposal.SubmittedAt,
            UpdatedAt = proposal.UpdatedAt,
            IsAigenerated = proposal.IsAigenerated
        };
    }
}
