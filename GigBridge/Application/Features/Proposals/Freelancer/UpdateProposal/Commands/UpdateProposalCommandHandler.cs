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

        if (proposal.Status != 0)
        {
            throw new Exception("Only pending proposal can be updated.");
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

        _context.Set<ProposalWorkBreakdownItem>().RemoveRange(proposal.ProposalWorkBreakdownItems);
        _context.Set<ProposalMilestonePlan>().RemoveRange(proposal.ProposalMilestonePlans);

        proposal.ProposalWorkBreakdownItems = (command.Request.WorkBreakdownItems ?? [])
            .Select((item, index) => ProposalPlanMapper.ToEntity(proposal.ProposalsId, item, index))
            .ToList();
        proposal.ProposalMilestonePlans = milestonePlans
            .Select((item, index) => ProposalPlanMapper.ToEntity(proposal.ProposalsId, item, index))
            .ToList();

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
