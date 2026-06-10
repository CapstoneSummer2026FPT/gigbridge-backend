using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
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

        proposal.CoverLetter = string.IsNullOrWhiteSpace(command.Request.CoverLetter)
            ? null
            : command.Request.CoverLetter.Trim();

        proposal.ProposedBudget = command.Request.ProposedBudget;

        proposal.ProposedDuration = string.IsNullOrWhiteSpace(command.Request.ProposedDuration)
            ? null
            : command.Request.ProposedDuration.Trim();

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
