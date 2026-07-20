using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Completion.Common.Internal;

internal static class FinalPayoutWorkflow
{
    public static async Task<FinalPayoutResult> ReleaseRemainingAsync(
        IApplicationDbContext context,
        Contract contract,
        ContractEscrow escrow,
        IReadOnlyCollection<Milestone> milestones,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var remaining = milestones
            .Select(item => (Milestone: item, Amount: Math.Max(0m, item.Amount - item.ReleasedAmount)))
            .Where(item => item.Amount > 0m)
            .ToList();
        var releaseVnd = remaining.Sum(item => item.Amount);
        if (releaseVnd <= 0m)
        {
            return default;
        }

        if (Math.Abs((escrow.FundedAmount - escrow.ReleasedAmount) - releaseVnd) > 0.01m)
        {
            throw new BadRequestException("Escrow balance is inconsistent with the final payout.");
        }

        var clientUserId = await context.Set<ClientProfile>()
            .Where(profile => profile.ClientProfilesId == contract.ClientProfilesId)
            .Select(profile => profile.UserId)
            .SingleAsync(cancellationToken);
        var freelancerUserId = await context.Set<FreelancerProfile>()
            .Where(profile => profile.FreelancerProfilesId == contract.FreelancerProfilesId)
            .Select(profile => profile.UserId)
            .SingleAsync(cancellationToken);
        var clientWallet = await context.Set<UserWallet>()
            .SingleOrDefaultAsync(wallet => wallet.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Client escrow wallet does not exist.");
        var freelancerWallet = await WalletWorkflow.GetOrCreateWalletAsync(
            context,
            freelancerUserId,
            now,
            cancellationToken);

        var releasedTokens = 0m;
        foreach (var item in remaining)
        {
            var code = $"ESCROW-FINAL-{escrow.ContractEscrowId:N}-{item.Milestone.MilestonesId:N}";
            var transfer = ContractEscrowWalletWorkflow.Release(
                context,
                clientWallet,
                freelancerWallet,
                contract.ContractsId,
                escrow.ContractEscrowId,
                item.Milestone.MilestonesId,
                item.Amount,
                code,
                "InternalTokenWallet",
                "Final 20% retention released to freelancer.",
                now);
            releasedTokens += transfer.GrossTokens;
            item.Milestone.ReleasedAmount = item.Milestone.Amount;
            item.Milestone.LastReleasedAt = now;
            item.Milestone.UpdatedAt = now;
            context.Set<EscrowTransaction>().Add(new EscrowTransaction
            {
                EscrowTransactionId = Guid.NewGuid(),
                ContractEscrowId = escrow.ContractEscrowId,
                MilestonesId = item.Milestone.MilestonesId,
                Amount = item.Amount,
                Type = (int)EscrowTransactionType.ReleaseToFreelancer,
                Status = (int)EscrowTransactionStatus.Succeeded,
                PaymentGateway = "InternalTokenWallet",
                GatewayTransactionCode = code,
                Note = "Final 20% retention released to freelancer.",
                CreatedAt = now,
                CompletedAt = now
            });
        }

        escrow.ReleasedAmount = escrow.FundedAmount;
        escrow.Status = (int)ContractEscrowStatus.Released;
        escrow.ReleasedAt = now;
        return new FinalPayoutResult(releaseVnd, releasedTokens);
    }
}

internal readonly record struct FinalPayoutResult(decimal ReleasedVnd, decimal ReleasedTokens);
