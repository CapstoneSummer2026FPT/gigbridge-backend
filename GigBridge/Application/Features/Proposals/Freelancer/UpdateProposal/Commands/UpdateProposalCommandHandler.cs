using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Scheduling;
using Application.Features.Proposals.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.UpdateProposal.Commands;

public class UpdateProposalCommandHandler : IRequestHandler<UpdateProposalCommand, bool>
{
    private const int MaxSaveAttempts = 3;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateProposalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(
        UpdateProposalCommand command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
        {
            try
            {
                await ApplyUpdateAsync(command, cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // A resumed question flow can briefly race a draft autosave/status
                // update. Drop stale tracked entities and reload the draft before
                // applying the freelancer's latest edit again.
                _context.ResetChangeTracker();

                if (attempt == MaxSaveAttempts - 1)
                {
                    throw new ConflictException(
                        "The proposal was updated in another request. Please retry submitting it.");
                }
            }
        }

        return false;
    }

    private async Task ApplyUpdateAsync(
        UpdateProposalCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ProposalLock.ForProposal(command.ProposalId), cancellationToken);

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => profile.UserId == command.UserId,
                cancellationToken);

        if (freelancerProfile is null)
        {
            throw new NotFoundException("Freelancer profile does not exist.");
        }

        var proposal = await _context.Set<Proposal>()
            .Include(item => item.ProposalWorkBreakdownItems)
            .Include(item => item.ProposalMilestonePlans)
            .Include(item => item.JobPosts)
            .FirstOrDefaultAsync(
                proposal =>
                    proposal.ProposalsId == command.ProposalId &&
                    proposal.FreelancerProfilesId == freelancerProfile.FreelancerProfilesId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist or you do not have permission to update it.");
        }

        ProposalModerationGuard.EnsureActive(proposal);

        if (proposal.Status != (int)Domain.Enums.Proposals.ProposalStatus.Draft)
        {
            throw new BadRequestException("Only draft proposals can be updated.");
        }

        var milestonePlans = (command.Request.MilestonePlans ?? []).OrderBy(item => item.OrderIndex).ToList();

        proposal.CoverLetter = string.IsNullOrWhiteSpace(command.Request.CoverLetter)
            ? null
            : command.Request.CoverLetter.Trim();

        proposal.ProposedBudget = ProposalTotalsCalculator.ResolveBudget(command.Request.ProposedBudget, milestonePlans);
        proposal.ProposedDuration = ProposalTotalsCalculator.ResolveDuration(command.Request.ProposedDuration, milestonePlans);

        proposal.AnalysisSummary = ProposalPlanMapper.Clean(command.Request.AnalysisSummary);
        proposal.SolutionApproach = ProposalPlanMapper.Clean(command.Request.SolutionApproach);
        proposal.Deliverables = ProposalPlanMapper.Clean(command.Request.Deliverables);
        proposal.Assumptions = ProposalPlanMapper.Clean(command.Request.Assumptions);
        proposal.OutOfScope = ProposalPlanMapper.Clean(command.Request.OutOfScope);

        var newMilestonePlans = milestonePlans
            .Select((item, index) => ProposalPlanMapper.ToEntity(proposal.ProposalsId, item, index))
            .ToList();
        var jobPostEndDate = proposal.JobPosts.EndDate.HasValue
            ? DateOnly.FromDateTime(proposal.JobPosts.EndDate.Value)
            : (DateOnly?)null;
        var computedDueDates = MilestoneDeadlineCalculator.CalculateDueDates(
            jobPostEndDate,
            milestonePlans.Select(item => item.EstimatedDuration).ToList());
        for (var i = 0; i < newMilestonePlans.Count; i++)
        {
            newMilestonePlans[i].DueDate = computedDueDates[i];
        }
        var milestoneIdsByOrder = newMilestonePlans.ToDictionary(item => item.OrderIndex, item => item.ProposalMilestonePlansId);
        var newWorkItems = ProposalPlanMapper.ResolveWorkItems(command.Request.WorkBreakdownItems, milestonePlans)
            .Select((item, index) => ProposalPlanMapper.ToEntity(
                proposal.ProposalsId,
                item,
                index,
                item.MilestoneOrderIndex.HasValue && milestoneIdsByOrder.TryGetValue(item.MilestoneOrderIndex.Value, out var milestoneId)
                    ? milestoneId
                    : null))
            .ToList();

        // RemoveRange already marks these entities Deleted. Do not also Clear() the
        // loaded navigation collections afterward: because ProposalWorkBreakdownItem's
        // FK to ProposalMilestonePlan is SetNull (not Cascade), doing so re-triggers EF
        // Core relationship fixup on the already-Deleted work items, flipping their
        // tracked state and causing the generated DELETE to affect 0 rows (surfaced as
        // a spurious DbUpdateConcurrencyException on every edit past the first).
        _context.Set<ProposalWorkBreakdownItem>().RemoveRange(proposal.ProposalWorkBreakdownItems);
        _context.Set<ProposalMilestonePlan>().RemoveRange(proposal.ProposalMilestonePlans);

        _context.Set<ProposalMilestonePlan>().AddRange(newMilestonePlans);
        _context.Set<ProposalWorkBreakdownItem>().AddRange(newWorkItems);

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
