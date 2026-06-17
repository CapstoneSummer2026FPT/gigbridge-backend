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

        if (proposal.Status == 3 || proposal.Status == 4 || proposal.Status == 5)
        {
            throw new Exception("Only draft, pending or shortlisted proposal can be updated.");
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

    private async Task UpdateStatusByClient(
    Proposal proposal,
    int requestedStatus)
    {
        if (proposal.Status == 0)
        {
            throw new BadRequestException("Client cannot update draft proposal.");
        }
        // 2 = Shortlisted, 3 = Accepted, 4 = Rejected
        if (requestedStatus != 2 && requestedStatus != 3 && requestedStatus != 4)
        {
            throw new UnauthorizedAccessException(
                "Client can only update proposal status to Shortlisted, Accepted, or Rejected.");
        }

        proposal.Status = requestedStatus;

        // 3 = Accepted
        if (requestedStatus == 3)
        {
            proposal.JobPosts.Status = 3;
            proposal.JobPosts.UpdatedAt = _dateTimeService.UtcNow;

        }
    }


    private static void UpdateStatusByFreelancer(
    Proposal proposal,
    int requestedStatus)
    {
        // Draft -> Pending: Freelancer submit draft proposal
        if (proposal.Status == 0 && requestedStatus == 1)
        {
            proposal.Status = 1;
            return;
        }

        // Pending/Shortlisted -> Withdrawn: Freelancer withdraw proposal
        if ((proposal.Status == 1 || proposal.Status == 2) && requestedStatus == 5)
        {
            proposal.Status = 5;
            return;
        }

        throw new UnauthorizedAccessException(
            "Freelancer can only submit a draft proposal or withdraw a pending/shortlisted proposal.");
    }
}
