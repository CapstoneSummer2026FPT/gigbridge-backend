using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Milestones.Common.Internal;

internal static class MilestoneApprovalWorkflow
{
    private const decimal InitialReleaseRate = 0.8m;

    public static async Task ReleaseAsync(
        IApplicationDbContext context,
        Contract contract,
        Milestone milestone,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var releaseCap = decimal.Round(
            milestone.Amount * InitialReleaseRate,
            2,
            MidpointRounding.AwayFromZero);
        var amount = releaseCap - milestone.ReleasedAmount;
        if (amount <= 0) return;

        var escrow = await context.Set<ContractEscrow>()
            .FirstOrDefaultAsync(item => item.ContractsId == contract.ContractsId, cancellationToken)
            ?? throw new NotFoundException("Contract escrow does not exist.");
        if (escrow.Status is not ((int)ContractEscrowStatus.Funded) and not ((int)ContractEscrowStatus.PartiallyReleased))
            throw new BadRequestException("Escrow must be funded before milestone approval.");
        if (escrow.FundedAmount - escrow.ReleasedAmount < amount)
            throw new BadRequestException("Escrow balance is insufficient for milestone release.");

        var clientUserId = await context.Set<ClientProfile>()
            .Where(item => item.ClientProfilesId == contract.ClientProfilesId)
            .Select(item => item.UserId)
            .FirstAsync(cancellationToken);
        var freelancerUserId = await context.Set<FreelancerProfile>()
            .Where(item => item.FreelancerProfilesId == contract.FreelancerProfilesId)
            .Select(item => item.UserId)
            .FirstAsync(cancellationToken);
        var clientWallet = await context.Set<UserWallet>()
            .FirstOrDefaultAsync(item => item.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        var freelancerWallet = await context.Set<UserWallet>()
            .FirstOrDefaultAsync(item => item.UserId == freelancerUserId, cancellationToken);
        if (freelancerWallet is null)
        {
            freelancerWallet = new UserWallet
            {
                UserWalletsId = Guid.NewGuid(),
                UserId = freelancerUserId,
                CreatedAt = now
            };
            context.Set<UserWallet>().Add(freelancerWallet);
        }

        var code = $"ESCROW-APPROVE-{escrow.ContractEscrowId:N}-{milestone.MilestonesId:N}";
        ContractEscrowWalletWorkflow.Release(
            context,
            clientWallet,
            freelancerWallet,
            contract.ContractsId,
            escrow.ContractEscrowId,
            milestone.MilestonesId,
            amount,
            code,
            "InternalTokenWallet",
            "Released automatically when the client approved the milestone.",
            now);
        milestone.ReleasedAmount += amount;
        milestone.LastReleasedAt = now;
        escrow.ReleasedAmount += amount;
        escrow.Status = escrow.ReleasedAmount >= escrow.FundedAmount
            ? (int)ContractEscrowStatus.Released
            : (int)ContractEscrowStatus.PartiallyReleased;
        escrow.ReleasedAt = escrow.Status == (int)ContractEscrowStatus.Released ? now : null;

        context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractEscrowId = escrow.ContractEscrowId,
            MilestonesId = milestone.MilestonesId,
            Amount = amount,
            Type = (int)EscrowTransactionType.ReleaseToFreelancer,
            Status = (int)EscrowTransactionStatus.Succeeded,
            PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = code,
            Note = "Released automatically when the client approved the milestone.",
            CreatedAt = now,
            CompletedAt = now
        });
    }
}
