using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;

public sealed class WithdrawMilestoneCommandHandler :
    IRequestHandler<WithdrawMilestoneCommand, WithdrawMilestoneResponse>
{
    public const decimal NormalFreelancerReleasePercentage = 0.8m;

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public WithdrawMilestoneCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<WithdrawMilestoneResponse> Handle(
        WithdrawMilestoneCommand command,
        CancellationToken cancellationToken)
    {
        var contract = await MilestoneWorkflowGuard.GetContractAsync(
            _context,
            command.ContractId,
            cancellationToken);
        MilestoneWorkflowGuard.EnsureContractActive(contract);
        await MilestoneWorkflowGuard.EnsureFreelancerAsync(
            _context,
            contract,
            command.UserId,
            cancellationToken);

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        await transaction.AcquireTransactionLockAsync(
            ContractEscrowLock.ForContract(command.ContractId), cancellationToken);

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.Approved)
        {
            throw new BadRequestException("Only approved milestones can be withdrawn.");
        }

        var releaseCapVnd = decimal.Round(
            milestone.Amount * NormalFreelancerReleasePercentage,
            2,
            MidpointRounding.AwayFromZero);
        var releasableVnd = releaseCapVnd - milestone.ReleasedAmount;
        if (releasableVnd <= 0m)
        {
            throw new ConflictException("This milestone has no remaining withdrawable amount.");
        }

        var milestones = await _context.Set<Milestone>()
            .Where(item => item.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);
        var requiredApprovedCount = (int)Math.Ceiling(milestones.Count * 0.5m);
        var approvedCount = milestones.Count(item =>
            item.Status is (int)MilestoneStatus.Approved or (int)MilestoneStatus.Completed);
        if (approvedCount < requiredApprovedCount)
        {
            throw new BadRequestException("At least 50% of contract milestones must be approved before withdrawal.");
        }

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(
                item => item.ContractsId == contract.ContractsId,
                cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");

        if (escrow.Status != (int)ContractEscrowStatus.Funded &&
            escrow.Status != (int)ContractEscrowStatus.PartiallyReleased)
        {
            throw new BadRequestException("Escrow must be funded before milestone withdrawal.");
        }

        if (escrow.FundedAmount - escrow.ReleasedAmount < releasableVnd)
        {
            throw new BadRequestException("Escrow balance is insufficient for this withdrawal.");
        }

        var clientUserId = await _context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (clientUserId == Guid.Empty)
        {
            throw new NotFoundException("Contract client profile does not exist.");
        }

        var clientWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        // Contract/escrow amounts are G-coin: the release amount is already in tokens.
        var grossTokens = releasableVnd;
        if (clientWallet.HeldTokens < grossTokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this withdrawal.");
        }

        var now = _dateTimeService.UtcNow;
        var freelancerWallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            command.UserId,
            now,
            cancellationToken);
        var transactionCode = $"ESCROW-RELEASE-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
        var transfer = ContractEscrowWalletWorkflow.Release(
            _context,
            clientWallet,
            freelancerWallet,
            escrow,
            contract.ContractsId,
            milestone.MilestonesId,
            releasableVnd,
            transactionCode,
            "InternalTokenWallet",
            "Released early from escrow to freelancer wallet.",
            now);

        milestone.ReleasedAmount += releasableVnd;
        milestone.LastReleasedAt = now;
        milestone.UpdatedAt = now;
        escrow.ReleasedAmount += releasableVnd;
        escrow.Status = escrow.ReleasedAmount >= escrow.FundedAmount
            ? (int)ContractEscrowStatus.Released
            : (int)ContractEscrowStatus.PartiallyReleased;
        escrow.ReleasedAt = escrow.Status == (int)ContractEscrowStatus.Released
            ? now
            : escrow.ReleasedAt;
        contract.UpdatedAt = now;

        _context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            Amount = releasableVnd,
            Type = (int)EscrowTransactionType.ReleaseToFreelancer,
            Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode,
            Note = "Normal freelancer early milestone withdrawal at 80% cap.",
            CreatedAt = now,
            CompletedAt = now
        });

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone early withdrawal released: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WithdrawMilestoneResponse(
            contract.ContractsId,
            milestone.MilestonesId,
            escrow.ContractEscrowId,
            releasableVnd,
            transfer.GrossTokens,
            milestone.ReleasedAmount,
            escrow.ReleasedAmount,
            escrow.Status);
    }
}
