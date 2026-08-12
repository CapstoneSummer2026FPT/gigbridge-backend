using Domain.Enums.AiInterviews;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.GetMyProposalByJobPost.Queries;

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
            .Include(proposal => proposal.ProposalWorkBreakdownItems)
            .Include(proposal => proposal.ProposalMilestonePlans)
            .FirstOrDefaultAsync(
                proposal =>
                    proposal.JobPostsId == request.JobPostId &&
                    proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        var definition = await _context.Set<AiInterviewDefinition>()
            .AsNoTracking()
            .Where(d => d.JobPostId == request.JobPostId &&
                d.Status != AiInterviewDefinitionStatus.Closed)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.AiInterviewDefinitionsId })
            .FirstOrDefaultAsync(cancellationToken);

        bool hasAiInterview = definition is not null;
        bool aiInterviewCompleted = false;
        bool aiInterviewInProgress = false;
        Guid? aiInterviewDefinitionId = definition?.AiInterviewDefinitionsId;

        if (definition is not null)
        {
            var attempts = await _context.Set<AiInterviewAttempt>()
                .AsNoTracking()
                .Where(attempt => attempt.AiInterviewDefinitionId == definition.AiInterviewDefinitionsId &&
                    attempt.FreelancerUserId == freelancerProfile.UserId)
                .Select(attempt => attempt.Status)
                .ToListAsync(cancellationToken);

            aiInterviewCompleted = attempts.Any(status => status == AiInterviewAttemptStatus.Completed);
            aiInterviewInProgress = !aiInterviewCompleted && attempts.Any(status => status == AiInterviewAttemptStatus.InProgress);
        }

        return new ProposalDetailDto
        {
            ProposalId = proposal.ProposalsId,
            JobPostId = proposal.JobPostsId,
            JobPostTitle = proposal.JobPosts.Title,
            FreelancerProfileId = proposal.FreelancerProfilesId,
            FreelancerName = proposal.FreelancerProfiles.User.FullName,
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
            SubmittedAt = proposal.SubmittedAt,
            UpdatedAt = proposal.UpdatedAt,
            IsAigenerated = proposal.IsAigenerated,
            HasAiInterview = hasAiInterview,
            AiInterviewCompleted = aiInterviewCompleted,
            AiInterviewInProgress = aiInterviewInProgress,
            AiInterviewDefinitionId = aiInterviewDefinitionId
        };
    }
}
