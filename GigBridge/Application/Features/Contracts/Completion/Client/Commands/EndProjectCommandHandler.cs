using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Completion.Client.DTOs;
using Application.Features.Contracts.Completion.Common.Internal;
using Application.Features.Premium.Client.SmartTalentMatching.Feedback;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Completion.Client.Commands;

public sealed class EndProjectCommandHandler : IRequestHandler<EndProjectCommand, EndProjectResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

    public EndProjectCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IChatRealtimeNotifier chatRealtimeNotifier)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _chatRealtimeNotifier = chatRealtimeNotifier;
    }

    public async Task<EndProjectResponse> Handle(EndProjectCommand command, CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(item => item.ContractsId == command.ContractId, cancellationToken)
            ?? throw new NotFoundException("Contract does not exist.");

        var clientProfile = await ContractParticipantGuard.EnsureClientAsync(
            _context, contract, command.UserId, cancellationToken);

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");

        if (contract.Status == (int)ContractStatus.Completed)
        {
            return new EndProjectResponse(
                contract.ContractsId, contract.Status, 0m, 0m, escrow.ReleasedAmount, contract.CompletedAt);
        }

        if (contract.Status != (int)ContractStatus.Active)
            throw new BadRequestException("Only active contracts can be ended.");

        var freelancerProfile = await _context.Set<FreelancerProfile>()
            .FirstOrDefaultAsync(
                profile => contract.FreelancerProfilesId.HasValue &&
                    profile.FreelancerProfilesId == contract.FreelancerProfilesId.Value,
                cancellationToken)
            ?? throw new BadRequestException("Contract does not have a selected freelancer.");

        var milestones = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        if (milestones.Count == 0)
            throw new BadRequestException("Contract must have at least one milestone before ending the project.");
        if (milestones.Any(item => item.Status != (int)MilestoneStatus.Approved))
            throw new BadRequestException("All milestones must be approved before ending the project.");
        if (escrow.Status is (int)ContractEscrowStatus.Disputed or
            (int)ContractEscrowStatus.Cancelled or (int)ContractEscrowStatus.Refunded)
            throw new BadRequestException("Escrow is not eligible for final payout.");
        if (escrow.Status != (int)ContractEscrowStatus.Funded &&
            escrow.Status != (int)ContractEscrowStatus.PartiallyReleased &&
            escrow.Status != (int)ContractEscrowStatus.Released)
            throw new BadRequestException("Escrow must be funded before ending the project.");

        var milestoneTotal = milestones.Sum(item => item.Amount);
        if (Math.Abs(escrow.FundedAmount - milestoneTotal) > 0.01m)
            throw new BadRequestException("Escrow funding must match the contract milestone total.");

        var now = _dateTimeService.UtcNow;
        var payout = await FinalPayoutWorkflow.ReleaseRemainingAsync(
            _context,
            contract,
            escrow,
            milestones,
            now,
            cancellationToken);

        contract.Status = (int)ContractStatus.Completed;
        contract.CompletedAt = now;
        contract.UpdatedAt = now;

        await TalentMatchFeedbackWriter.TryAddLatestAttributedAsync(
            _context,
            contract.JobPostsId,
            freelancerProfile.FreelancerProfilesId,
            TalentMatchEventType.ContractCompleted,
            contract.ContractsId,
            now,
            cancellationToken);

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            "Project ended. Final 20% retention released to freelancer.",
            now,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            contractId = contract.ContractsId,
            status = contract.Status,
            escrowReleasedAmountVnd = escrow.ReleasedAmount,
            completedAt = contract.CompletedAt
        };
        var conversationIds = await _context.Set<Conversation>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .Select(item => item.ConversationsId)
            .ToListAsync(cancellationToken);
        foreach (var conversationId in conversationIds)
            await _chatRealtimeNotifier.SendConversationEventAsync(
                conversationId, "ContractCompleted", payload, cancellationToken);

        await _chatRealtimeNotifier.SendUsersEventAsync(
            new[] { clientProfile.UserId, freelancerProfile.UserId },
            "ContractCompleted",
            payload,
            cancellationToken);

        return new EndProjectResponse(
            contract.ContractsId,
            contract.Status,
            payout.ReleasedVnd,
            payout.ReleasedTokens,
            escrow.ReleasedAmount,
            contract.CompletedAt);
    }
}
