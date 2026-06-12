using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Proposals.Common.UpdateProposalStatus.Commands;

public class UpdateProposalStatusCommandHandler
    : IRequestHandler<UpdateProposalStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public UpdateProposalStatusCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<bool> Handle(
        UpdateProposalStatusCommand command,
        CancellationToken cancellationToken)
    {
        var proposal = await _context.Set<Proposal>()
            .Include(proposal => proposal.JobPosts)
            .FirstOrDefaultAsync(
                proposal => proposal.ProposalsId == command.ProposalId,
                cancellationToken);

        if (proposal is null)
        {
            throw new NotFoundException("Proposal does not exist.");
        }

        if (proposal.Status == 2 || proposal.Status == 3 || proposal.Status == 4)
        {
            throw new Exception("Only pending or shortlisted proposal can be updated.");
        }

        var requestedStatus = command.Request.Status;

        var isClientOwner = await _context.Set<ClientProfile>()
            .AnyAsync(
                clientProfile =>
                    clientProfile.UserId == command.UserId &&
                    clientProfile.ClientProfilesId == proposal.JobPosts.ClientProfilesId,
                cancellationToken);

        var isFreelancerOwner = await _context.Set<FreelancerProfile>()
            .AnyAsync(
                freelancerProfile =>
                    freelancerProfile.UserId == command.UserId &&
                    freelancerProfile.FreelancerProfilesId == proposal.FreelancerProfilesId,
                cancellationToken);

        if (isClientOwner)
        {
            UpdateStatusByClient(proposal, requestedStatus);
        }
        else if (isFreelancerOwner)
        {
            UpdateStatusByFreelancer(proposal, requestedStatus);
        }
        else
        {
            throw new UnauthorizedAccessException("You do not have permission to update this proposal.");
        }

        proposal.UpdatedAt = _dateTimeService.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void UpdateStatusByClient(
        Proposal proposal,
        int requestedStatus)
    {
        if (requestedStatus == 2)
        {
            throw new BadRequestException("Accepting a proposal must go through negotiation final offer flow.");
        }

        if (requestedStatus != 1 && requestedStatus != 3)
        {
            throw new UnauthorizedAccessException(
                "Client can only update proposal status to Shortlisted or Rejected.");
        }

        proposal.Status = requestedStatus;
    }

    private static void UpdateStatusByFreelancer(
        Proposal proposal,
        int requestedStatus)
    {
        if (requestedStatus != 4)
        {
            throw new UnauthorizedAccessException(
                "Freelancer can only withdraw their own proposal.");
        }

        proposal.Status = 4;
    }
}
