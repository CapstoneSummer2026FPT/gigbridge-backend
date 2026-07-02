using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Completion.Freelancer.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Completion.Freelancer.Commands;

public sealed class ClaimFinalPayoutCommandHandler : IRequestHandler<ClaimFinalPayoutCommand, ClaimFinalPayoutResponse>
{
    private const string GatewayProvider = "InternalTokenWallet";
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

        var remaining = milestones.Select(item => new
        {
            Milestone = item,
            Amount = Math.Max(0m, item.Amount - item.ReleasedAmount)
        }).ToList();
        var releaseVnd = remaining.Sum(item => item.Amount);

        // Compatibility for projects completed by the previous implementation.
        if (releaseVnd <= 0m && escrow.ReleasedAmount >= escrow.FundedAmount)
        {
            return new ClaimFinalPayoutResponse(
                contract.ContractsId, 0m, 0m, escrow.ReleasedAmount,
                escrow.Status, true, escrow.ReleasedAt ?? contract.CompletedAt);
        }

        if (releaseVnd <= 0m)
            throw new BadRequestException("Escrow release amounts are inconsistent.");
        if (Math.Abs((escrow.FundedAmount - escrow.ReleasedAmount) - releaseVnd) > 0.01m)
            throw new BadRequestException("Escrow balance is inconsistent with milestone release amounts.");

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (clientUserId == Guid.Empty)
            throw new NotFoundException("Contract client profile does not exist.");

        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        var freelancerWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == freelancer.UserId, cancellationToken);
        var now = _dateTimeService.UtcNow;
        var releaseTokens = releaseVnd;
        if (clientWallet.HeldTokens < releaseTokens)
            throw new BadRequestException("Client held wallet balance is insufficient for final payout.");

        if (freelancerWallet is null)
        {
            freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(), UserId = freelancer.UserId,
                AvailableTokens = 0m, HeldTokens = 0m, CreatedAt = now
            };
            _context.Set<UserWallet>().Add(freelancerWallet);
        }

        clientWallet.HeldTokens -= releaseTokens;
        clientWallet.UpdatedAt = now;
        freelancerWallet.AvailableTokens += releaseTokens;
        freelancerWallet.UpdatedAt = now;

        foreach (var item in remaining)
        {
            item.Milestone.ReleasedAmount = item.Milestone.Amount;
            item.Milestone.LastReleasedAt = now;
            item.Milestone.UpdatedAt = now;
            if (item.Amount <= 0m) continue;

            var itemTokens = item.Amount;
            var code = $"ESCROW-FINAL-CLAIM-{escrow.ContractEscrowId:N}-{item.Milestone.MilestonesId:N}";
            AddWalletTransaction(clientWallet, contract, escrow, item.Milestone, item.Amount, itemTokens, code,
                "Final payout released from client held wallet.", now);
            AddWalletTransaction(freelancerWallet, contract, escrow, item.Milestone, item.Amount, itemTokens, code,
                "Final payout claimed by freelancer.", now);
            _context.Set<EscrowTransaction>().Add(new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
                MilestonesId = item.Milestone.MilestonesId, Amount = item.Amount,
                Type = (int)EscrowTransactionType.ReleaseToFreelancer,
                Status = (int)EscrowTransactionStatus.Succeeded,
                PaymentGateway = GatewayProvider, GatewayTransactionCode = code,
                Note = "Final payout claimed by freelancer.", CreatedAt = now, CompletedAt = now
            });
        }

        escrow.ReleasedAmount = escrow.FundedAmount;
        escrow.Status = (int)ContractEscrowStatus.Released;
        escrow.ReleasedAt = now;
        contract.UpdatedAt = now;
        await ContractConversationEvents.AddSystemMessageAsync(
            _context, contract.ContractsId, "Final payout claimed by freelancer.", now, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var payload = new
        {
            contractId = contract.ContractsId, releasedAmountVnd = releaseVnd,
            releasedTokens = releaseTokens, escrowReleasedAmountVnd = escrow.ReleasedAmount,
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
            contract.ContractsId, releaseVnd, releaseTokens, escrow.ReleasedAmount,
            escrow.Status, false, now);
    }

    private void AddWalletTransaction(
        UserWallet wallet, Contract contract, ContractEscrow escrow, Milestone milestone,
        decimal amountVnd, decimal tokens, string code, string note, DateTime now)
    {
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(), UserWalletsId = wallet.UserWalletsId,
            UserId = wallet.UserId, ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId, MilestonesId = milestone.MilestonesId,
            TokenAmount = tokens, VndAmount = amountVnd, Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded, IdempotencyKey = code,
            GatewayProvider = GatewayProvider, GatewayTransactionCode = code, Note = note,
            CreatedAt = now, CompletedAt = now
        });
    }
}
