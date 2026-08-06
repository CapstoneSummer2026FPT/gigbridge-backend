using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Proposals.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Freelancer.UpdateProposal.Commands;

public class UpdateProposalCommandHandler : IRequestHandler<UpdateProposalCommand, bool>
{
    private const int MaxSaveAttempts = 2;

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
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts - 1)
            {
                // Npgsql guards every row with the implicit xmin version column. If a
                // duplicate submit or a racing status update commits first, the save
                // affects 0 rows. The freelancer's latest edit wins, so reload and retry.
                _context.ResetChangeTracker();
            }
        }

        return false;
    }

    private async Task ApplyUpdateAsync(
        UpdateProposalCommand command,
        CancellationToken cancellationToken)
    {
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

        if (proposal.Status != (int)Domain.Enums.ProposalStatus.Draft)
        {
            throw new BadRequestException("Only draft proposals can be updated.");
        }

        var milestonePlans = (command.Request.MilestonePlans ?? []).ToList();

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

        _context.Set<ProposalWorkBreakdownItem>().RemoveRange(proposal.ProposalWorkBreakdownItems);
        _context.Set<ProposalMilestonePlan>().RemoveRange(proposal.ProposalMilestonePlans);

        proposal.ProposalMilestonePlans.Clear();
        foreach (var milestone in newMilestonePlans)
        {
            proposal.ProposalMilestonePlans.Add(milestone);
        }

        proposal.ProposalWorkBreakdownItems.Clear();
        foreach (var item in newWorkItems)
        {
            proposal.ProposalWorkBreakdownItems.Add(item);
        }

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
