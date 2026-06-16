using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.Internal;
using Application.Features.Contracts.Milestones.Common.DTOs;
using Application.Features.Contracts.Milestones.Common.Internal;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Freelancer.Withdraw.Commands;

public sealed class WithdrawMilestoneCommandHandler :
    IRequestHandler<WithdrawMilestoneCommand, WithdrawMilestoneResponse>
{
    private const decimal NormalFreelancerReleasePercentage = 0.8m;

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

        var milestone = await MilestoneWorkflowGuard.GetMilestoneAsync(
            _context,
            command.ContractId,
            command.MilestoneId,
            cancellationToken);

        if (milestone.Status != (int)MilestoneStatus.Approved)
        {
            throw new BadRequestException("Only approved milestones can be withdrawn.");
        }

        var milestones = await _context.Set<Milestone>()
            .Where(milestone => milestone.ContractsId == contract.ContractsId)
            .ToListAsync(cancellationToken);

        var requiredApprovedCount = (int)Math.Ceiling(milestones.Count * 0.5m);
        var approvedCount = milestones.Count(milestone => milestone.Status == (int)MilestoneStatus.Approved);
        if (approvedCount < requiredApprovedCount)
        {
            throw new BadRequestException("At least 50% of contract milestones must be approved before withdrawal.");
        }

        var releaseCapVnd = decimal.Round(
            milestone.Amount * NormalFreelancerReleasePercentage,
            2,
            MidpointRounding.AwayFromZero);
        var releasableVnd = releaseCapVnd - milestone.ReleasedAmount;
        if (releasableVnd <= 0)
        {
            throw new ConflictException("This milestone has no remaining withdrawable amount.");
        }

        var escrow = await _context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(
                escrow => escrow.ContractsId == contract.ContractsId,
                cancellationToken);

        if (escrow is null)
        {
            throw new NotFoundException("Contract escrow does not exist.");
        }

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
            .FirstOrDefaultAsync(wallet => wallet.UserId == clientUserId, cancellationToken);

        if (clientWallet is null)
        {
            throw new BadRequestException("Client escrow wallet does not exist.");
        }

        var releasedTokens = TokenWalletRules.ToTokens(releasableVnd);
        if (clientWallet.HeldTokens < releasedTokens)
        {
            throw new BadRequestException("Client held wallet balance is insufficient for this withdrawal.");
        }

        var freelancerWallet = await _context.Set<UserWallet>()
            .FirstOrDefaultAsync(wallet => wallet.UserId == command.UserId, cancellationToken);

        var now = _dateTimeService.UtcNow;
        if (freelancerWallet is null)
        {
            freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = command.UserId,
                AvailableTokens = 0m,
                HeldTokens = 0m,
                CreatedAt = now
            };
            _context.Set<UserWallet>().Add(freelancerWallet);
        }

        clientWallet.HeldTokens -= releasedTokens;
        clientWallet.UpdatedAt = now;
        freelancerWallet.AvailableTokens += releasedTokens;
        freelancerWallet.UpdatedAt = now;

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

        var transactionCode = $"ESCROW-RELEASE-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = clientWallet.UserWalletsId,
            UserId = clientWallet.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            TokenAmount = releasedTokens,
            VndAmount = releasableVnd,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode,
            Note = "Released from client held escrow to freelancer.",
            CreatedAt = now,
            CompletedAt = now
        });
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(),
            UserWalletsId = freelancerWallet.UserWalletsId,
            UserId = freelancerWallet.UserId,
            ContractsId = contract.ContractsId,
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            TokenAmount = releasedTokens,
            VndAmount = releasableVnd,
            Type = (int)WalletTransactionType.EscrowRelease,
            Status = (int)WalletTransactionStatus.Succeeded,
            GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode,
            Note = "Released from escrow to freelancer wallet.",
            CreatedAt = now,
            CompletedAt = now
        });
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
            Note = "Normal freelancer milestone withdrawal at 80% cap.",
            CreatedAt = now,
            CompletedAt = now
        });

        await ContractConversationEvents.AddSystemMessageAsync(
            _context,
            contract.ContractsId,
            $"Milestone withdrawal released: {milestone.Title}.",
            now,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new WithdrawMilestoneResponse(
            contract.ContractsId,
            milestone.MilestonesId,
            escrow.ContractEscrowId,
            releasableVnd,
            releasedTokens,
            milestone.ReleasedAmount,
            escrow.ReleasedAmount,
            escrow.Status);
    }
}
