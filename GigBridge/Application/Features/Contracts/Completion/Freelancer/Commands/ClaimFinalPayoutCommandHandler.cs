using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Completion.Common.Internal;
using Application.Features.Contracts.Completion.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Completion.Freelancer.Commands;

public sealed class ClaimFinalPayoutCommandHandler : IRequestHandler<ClaimFinalPayoutCommand, ClaimFinalPayoutResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public ClaimFinalPayoutCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<ClaimFinalPayoutResponse> Handle(
        ClaimFinalPayoutCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(item => item.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");
        var freelancer = await ContractParticipantGuard.EnsureFreelancerAsync(
            _context, contract, command.UserId, cancellationToken);

        if (contract.Status != (int)ContractStatus.Completed)
            throw new BadRequestException("The project must be completed before claiming the final payout.");

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");
        if (escrow.Status is (int)ContractEscrowStatus.Disputed or
            (int)ContractEscrowStatus.Cancelled or (int)ContractEscrowStatus.Refunded)
            throw new BadRequestException("Escrow is not eligible for final payout.");
        if (escrow.Status != (int)ContractEscrowStatus.Funded &&
            escrow.Status != (int)ContractEscrowStatus.PartiallyReleased &&
            escrow.Status != (int)ContractEscrowStatus.Released)
            throw new BadRequestException("Escrow has not been funded.");

        var milestones = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        if (milestones.Count == 0 || milestones.Any(item => item.Status != (int)MilestoneStatus.Approved))
            throw new BadRequestException("All milestones must be approved before claiming the final payout.");

        var milestoneTotal = milestones.Sum(item => item.Amount);
        if (Math.Abs(escrow.FundedAmount - milestoneTotal) > 0.01m)
            throw new BadRequestException("Escrow funding must match the contract milestone total.");

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (clientUserId == Guid.Empty)
            throw new NotFoundException("Contract client profile does not exist.");

        var now = _dateTimeService.UtcNow;
        var payout = await FinalPayoutWorkflow.ReleaseRemainingAsync(
            _context,
            contract,
            escrow,
            milestones,
            now,
            cancellationToken);
        if (payout.ReleasedVnd <= 0m)
        {
            return new ClaimFinalPayoutResponse(
                contract.ContractsId,
                0m,
                0m,
                escrow.ReleasedAmount,
                escrow.Status,
                true,
                escrow.ReleasedAt ?? contract.CompletedAt);
        }

        contract.UpdatedAt = now;
        await ContractConversationEvents.AddSystemMessageAsync(
            _context, contract.ContractsId, "Final payout claimed by freelancer.", now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var payload = new
        {
            contractId = contract.ContractsId, releasedAmountVnd = payout.ReleasedVnd,
            releasedTokens = payout.ReleasedTokens, escrowReleasedAmountVnd = escrow.ReleasedAmount,
            escrowStatus = escrow.Status, claimedAt = now
        };
        var conversationIds = await _context.Set<Conversation>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .Select(item => item.ConversationsId)
            .ToListAsync(cancellationToken);
        foreach (var conversationId in conversationIds)
            await _chatRealtimeNotifier.SendConversationEventAsync(
                conversationId, "FinalPayoutClaimed", payload, cancellationToken);
        await _chatRealtimeNotifier.SendUsersEventAsync(
            new[] { clientUserId, freelancer.UserId }, "FinalPayoutClaimed", payload, cancellationToken);

        return new ClaimFinalPayoutResponse(
            contract.ContractsId, payout.ReleasedVnd, payout.ReleasedTokens, escrow.ReleasedAmount,
            escrow.Status, false, now);
    }
}
