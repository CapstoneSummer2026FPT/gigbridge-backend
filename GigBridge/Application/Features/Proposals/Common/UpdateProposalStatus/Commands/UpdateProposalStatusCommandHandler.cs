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
            await UpdateStatusByClient(proposal, requestedStatus, cancellationToken);
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
        int requestedStatus,
        CancellationToken cancellationToken)
    {
        if (requestedStatus != 1 && requestedStatus != 2 && requestedStatus != 3)
        {
            throw new UnauthorizedAccessException(
                "Client can only update proposal status to Shortlisted, Accepted, or Rejected.");
        }

        proposal.Status = requestedStatus;

        if (requestedStatus == 2)
        {
            var otherProposals = await _context.Set<Proposal>()
                .Where(otherProposal =>
                    otherProposal.JobPostsId == proposal.JobPostsId &&
                    otherProposal.ProposalsId != proposal.ProposalsId &&
                    (otherProposal.Status == 0 || otherProposal.Status == 1))
                .ToListAsync(cancellationToken);

            foreach (var otherProposal in otherProposals)
            {
                otherProposal.Status = 3;
                otherProposal.UpdatedAt = _dateTimeService.UtcNow;
            }

            proposal.JobPosts.Status = 2;
            proposal.JobPosts.UpdatedAt = _dateTimeService.UtcNow;

            await AttachAcceptedProposalToDraftContract(proposal, cancellationToken);
        }
    }

    private async Task AttachAcceptedProposalToDraftContract(
        Proposal proposal,
        CancellationToken cancellationToken)
    {
        if (!proposal.ProposedBudget.HasValue || proposal.ProposedBudget.Value <= 0)
        {
            throw new BadRequestException("Accepted proposals must include a proposed budget.");
        }

        var now = _dateTimeService.UtcNow;
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(
                contract => contract.JobPostsId == proposal.JobPostsId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract draft does not exist for this job post.");
        }

        if (contract.Status != (int)ContractStatus.Draft &&
            contract.Status != (int)ContractStatus.PendingFreelancerSelection)
        {
            throw new BadRequestException("Only draft contracts can be attached to an accepted proposal.");
        }

        contract.FreelancerProfilesId = proposal.FreelancerProfilesId;
        contract.ProposalsId = proposal.ProposalsId;
        contract.TotalBudget = proposal.ProposedBudget.Value;
        contract.Status = (int)ContractStatus.PendingEscrow;
        contract.StartDate ??= DateOnly.FromDateTime(now);
        contract.EndDate ??= proposal.JobPosts.EndDate.HasValue
            ? DateOnly.FromDateTime(proposal.JobPosts.EndDate.Value)
            : null;
        contract.UpdatedAt = now;

        var escrowExists = await _context.Set<ContractEscrow>()
            .AnyAsync(
                escrow => escrow.ContractsId == contract.ContractsId,
                cancellationToken);

        if (escrowExists)
        {
            return;
        }

        _context.Set<ContractEscrow>().Add(new ContractEscrow
        {
            ContractEscrowId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            RequiredAmount = contract.TotalBudget * 0.8m,
            FundedAmount = 0m,
            RequiredPercentage = 0.8m,
            Currency = "VND",
            Status = (int)ContractEscrowStatus.PendingFunding,
            CreatedAt = now
        });
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
