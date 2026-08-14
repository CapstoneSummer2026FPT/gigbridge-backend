using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Scheduling;
using Application.Features.JobPosts.Common;
using Application.Features.Proposals.Common;
using Application.Features.Proposals.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.SubmitProposal.Commands;

public class SubmitProposalCommandHandler : IRequestHandler<SubmitProposalCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public SubmitProposalCommandHandler(IApplicationDbContext context, IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> Handle(SubmitProposalCommand command, CancellationToken cancellationToken)
    {
        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(profile => profile.UserId == command.UserId, cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var jobPost = await _context.Set<JobPost>()
            .AsNoTracking()
            .Include(job => job.JobPostMilestonePlans)
                .ThenInclude(milestone => milestone.WorkItems)
            .FirstOrDefaultAsync(job => job.JobPostsId == command.Request.JobPostsId, cancellationToken);

        if (jobPost is null)
        {
            throw new NotFoundException("Job post does not exist.");
        }

        EnsureJobPostAcceptsProposals(jobPost);
        await EnsureProposalHasNotBeenSubmittedAsync(command, freelancerProfile.FreelancerProfilesId, cancellationToken);

        var proposalId = Guid.NewGuid();
        var milestonePlans = (command.Request.MilestonePlans ?? []).OrderBy(item => item.OrderIndex).ToList();
        if (milestonePlans.Count == 0 && jobPost.JobPostMilestonePlans.Count > 0)
        {
            milestonePlans = jobPost.JobPostMilestonePlans.OrderBy(item => item.OrderIndex).Select(item => new ProposalMilestonePlanDto
            {
                Title = item.Title, Description = item.Description, Amount = item.Amount,
                EstimatedDuration = item.EstimatedDuration, Deliverables = item.Deliverables,
                DueDate = item.DueDate, AcceptanceCriteria = item.AcceptanceCriteria, OrderIndex = item.OrderIndex,
                WorkItems = item.WorkItems.OrderBy(workItem => workItem.OrderIndex).Select(workItem => new ProposalWorkBreakdownItemDto
                {
                    Title = workItem.Title, Description = workItem.Description, Deliverables = workItem.Deliverables,
                    EstimatedDuration = workItem.EstimatedDuration, OrderIndex = workItem.OrderIndex,
                    MilestoneOrderIndex = item.OrderIndex
                }).ToList()
            }).ToList();
        }
        var proposal = new Proposal
        {
            ProposalsId = proposalId,
            JobPostsId = command.Request.JobPostsId,
            FreelancerProfilesId = freelancerProfile.FreelancerProfilesId,
            CoverLetter = command.Request.CoverLetter?.Trim(),
            ProposedBudget = ProposalTotalsCalculator.ResolveBudget(command.Request.ProposedBudget, milestonePlans),
            ProposedDuration = ProposalTotalsCalculator.ResolveDuration(command.Request.ProposedDuration, milestonePlans),
            AnalysisSummary = ProposalPlanMapper.Clean(command.Request.AnalysisSummary),
            SolutionApproach = ProposalPlanMapper.Clean(command.Request.SolutionApproach),
            Deliverables = ProposalPlanMapper.Clean(command.Request.Deliverables),
            Assumptions = ProposalPlanMapper.Clean(command.Request.Assumptions),
            OutOfScope = ProposalPlanMapper.Clean(command.Request.OutOfScope),
            Status = 0,
            SubmittedAt = null,
            UpdatedAt = _dateTimeService.UtcNow
        };

        var milestonePlanEntities = milestonePlans
            .Select((item, index) => ProposalPlanMapper.ToEntity(proposalId, item, index))
            .ToList();
        var jobPostEndDate = jobPost.EndDate.HasValue ? DateOnly.FromDateTime(jobPost.EndDate.Value) : (DateOnly?)null;
        var computedDueDates = MilestoneDeadlineCalculator.CalculateDueDates(
            jobPostEndDate,
            milestonePlans.Select(item => item.EstimatedDuration).ToList());
        for (var i = 0; i < milestonePlanEntities.Count; i++)
        {
            milestonePlanEntities[i].DueDate = computedDueDates[i];
        }
        proposal.ProposalMilestonePlans = milestonePlanEntities;
        var milestoneIdsByOrder = proposal.ProposalMilestonePlans.ToDictionary(item => item.OrderIndex, item => item.ProposalMilestonePlansId);
        proposal.ProposalWorkBreakdownItems = ProposalPlanMapper.ResolveWorkItems(command.Request.WorkBreakdownItems, milestonePlans)
            .Select((item, index) => ProposalPlanMapper.ToEntity(
                proposalId,
                item,
                index,
                item.MilestoneOrderIndex.HasValue && milestoneIdsByOrder.TryGetValue(item.MilestoneOrderIndex.Value, out var milestoneId)
                    ? milestoneId
                    : null))
            .ToList();

        _context.Set<Proposal>().Add(proposal);
        await _context.SaveChangesAsync(cancellationToken);

        return proposal.ProposalsId;
    }

    private void EnsureJobPostAcceptsProposals(JobPost jobPost)
    {
        if (jobPost.Visibility == JobPostNegotiationGuard.AdminLockedVisibility)
        {
            throw new BadRequestException("This job post has been locked by an admin and is not accepting proposals.");
        }

        if (jobPost.Status != JobPostNegotiationGuard.OpenStatus)
        {
            throw new BadRequestException("This job post is not accepting proposals.");
        }

        if (jobPost.EndDate.HasValue && jobPost.EndDate.Value <= _dateTimeService.UtcNow)
        {
            throw new BadRequestException("This job post application deadline has passed.");
        }
    }

    private async Task EnsureProposalHasNotBeenSubmittedAsync(
        SubmitProposalCommand command,
        Guid freelancerProfileId,
        CancellationToken cancellationToken)
    {
        var hasSubmitted = await _context.Set<Proposal>()
            .AnyAsync(proposal =>
                proposal.JobPostsId == command.Request.JobPostsId &&
                proposal.FreelancerProfilesId == freelancerProfileId,
                cancellationToken);

        if (hasSubmitted)
        {
            throw new BadRequestException("You have already submitted a proposal for this job.");
        }
    }
}
