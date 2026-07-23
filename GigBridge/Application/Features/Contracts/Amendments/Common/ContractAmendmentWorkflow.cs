using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Contracts.Amendments.Common;

internal static class ContractAmendmentWorkflow
{
    public static async Task ApplyAsync(
        IApplicationDbContext context,
        IDateTimeService clock,
        Contract contract,
        ContractAmendment amendment,
        CancellationToken cancellationToken)
    {
        if (amendment.Status == (int)ContractAmendmentStatus.Applied)
        {
            return;
        }

        var pendingMilestones = await context.Set<Milestone>()
            .Include(item => item.WorkItems)
            .Where(item => item.ContractsId == contract.ContractsId && item.Status == (int)MilestoneStatus.Pending)
            .ToListAsync(cancellationToken);

        context.Set<ContractWorkItem>().RemoveRange(pendingMilestones.SelectMany(item => item.WorkItems));
        context.Set<Milestone>().RemoveRange(pendingMilestones);

        var now = clock.UtcNow;
        foreach (var snapshot in amendment.Milestones.OrderBy(item => item.OrderIndex))
        {
            var milestone = new Milestone
            {
                MilestonesId = Guid.NewGuid(),
                ContractsId = contract.ContractsId,
                Title = snapshot.Title,
                Description = snapshot.Description,
                Amount = snapshot.Amount,
                EstimatedDuration = snapshot.EstimatedDuration,
                DueDate = snapshot.DueDate,
                Deliverables = snapshot.Deliverables,
                AcceptanceCriteria = snapshot.AcceptanceCriteria,
                SortOrder = snapshot.OrderIndex,
                Status = (int)MilestoneStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Set<Milestone>().Add(milestone);

            foreach (var workItemSnapshot in snapshot.WorkItems.OrderBy(item => item.OrderIndex))
            {
                context.Set<ContractWorkItem>().Add(new ContractWorkItem
                {
                    ContractWorkItemId = Guid.NewGuid(),
                    MilestonesId = milestone.MilestonesId,
                    Title = workItemSnapshot.Title,
                    Description = workItemSnapshot.Description,
                    Deliverables = workItemSnapshot.Deliverables,
                    EstimatedDuration = workItemSnapshot.EstimatedDuration,
                    OrderIndex = workItemSnapshot.OrderIndex,
                    Status = (int)ContractWorkItemStatus.Todo,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        contract.TotalBudget = amendment.ProposedTotalBudget;
        contract.RevisionNumber = amendment.RevisionNumber;
        contract.UpdatedAt = now;
        amendment.Status = (int)ContractAmendmentStatus.Applied;
        amendment.AppliedAt = now;
    }

    public static async Task RefundDecreaseAsync(
        IApplicationDbContext context,
        IDateTimeService clock,
        Contract contract,
        ContractAmendment amendment,
        CancellationToken cancellationToken)
    {
        var refund = -amendment.BudgetDelta;
        if (refund <= 0)
        {
            return;
        }

        var transactionCode = $"AMENDMENT-REFUND-{amendment.ContractAmendmentId:N}";
        if (await context.Set<WalletTransaction>().AnyAsync(item => item.GatewayTransactionCode == transactionCode, cancellationToken))
        {
            return;
        }

        var clientUserId = await context.Set<ClientProfile>()
            .Where(item => item.ClientProfilesId == contract.ClientProfilesId)
            .Select(item => item.UserId)
            .SingleAsync(cancellationToken);
        var wallet = await context.Set<UserWallet>()
            .SingleAsync(item => item.UserId == clientUserId, cancellationToken);
        var escrow = await context.Set<ContractEscrow>()
            .SingleAsync(item => item.ContractsId == contract.ContractsId, cancellationToken);

        if (escrow.FundedAmount - escrow.ReleasedAmount < refund)
        {
            throw new BadRequestException("Held escrow is insufficient for this amendment refund.");
        }

        ContractEscrowWalletWorkflow.Refund(
            context,
            wallet,
            contract.ContractsId,
            escrow.ContractEscrowId,
            null,
            refund,
            transactionCode,
            "InternalTokenWallet",
            "Contract amendment budget decrease.",
            clock.UtcNow);
        escrow.RequiredAmount -= refund;
        escrow.FundedAmount -= refund;
        context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            Amount = refund, Type = (int)EscrowTransactionType.RefundToClient,
            Status = (int)EscrowTransactionStatus.Succeeded, PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode, Note = "Contract amendment budget decrease.",
            CreatedAt = clock.UtcNow, CompletedAt = clock.UtcNow
        });
    }

    public static async Task FundIncreaseAsync(
        IApplicationDbContext context,
        IDateTimeService clock,
        Contract contract,
        ContractAmendment amendment,
        Guid clientUserId,
        CancellationToken cancellationToken)
    {
        var amount = amendment.BudgetDelta;
        if (amount <= 0)
        {
            throw new BadRequestException("This amendment does not require additional funding.");
        }

        var transactionCode = $"AMENDMENT-FUND-{amendment.ContractAmendmentId:N}";
        if (await context.Set<WalletTransaction>().AnyAsync(item => item.GatewayTransactionCode == transactionCode, cancellationToken))
        {
            await ApplyAsync(context, clock, contract, amendment, cancellationToken);
            return;
        }

        var wallet = await context.Set<UserWallet>()
            .SingleOrDefaultAsync(item => item.UserId == clientUserId, cancellationToken)
            ?? throw new BadRequestException("Wallet balance is insufficient to fund the amendment.");
        var escrow = await context.Set<ContractEscrow>()
            .SingleAsync(item => item.ContractsId == contract.ContractsId, cancellationToken);
        var tokens = TokenWalletRules.ToTokens(amount);
        WalletWorkflow.DebitAvailable(wallet, tokens, clock.UtcNow, "Wallet balance is insufficient to fund the amendment.");
        wallet.HeldTokens += tokens;
        await ServiceFeeWorkflow.ChargeAsync(
            context,
            clientUserId,
            contract.ContractsId,
            amount,
            $"{ServiceFeeWorkflow.ClientFundingFeePrefix}{amendment.ContractAmendmentId:N}",
            $"1% client service fee for funding contract amendment: {contract.Title}.",
            clock.UtcNow,
            cancellationToken);
        escrow.RequiredAmount += amount;
        escrow.FundedAmount += amount;

        context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            WalletTransactionsId = Guid.NewGuid(), UserWalletsId = wallet.UserWalletsId,
            UserId = clientUserId, ContractsId = contract.ContractsId, ContractEscrowId = escrow.ContractEscrowId,
            TokenAmount = tokens, VndAmount = amount, Type = (int)WalletTransactionType.EscrowHold,
            Status = (int)WalletTransactionStatus.Succeeded, GatewayProvider = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode, CreatedAt = clock.UtcNow, CompletedAt = clock.UtcNow
        });
        context.Set<EscrowTransaction>().Add(new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(), ContractEscrowId = escrow.ContractEscrowId,
            Amount = amount, Type = (int)EscrowTransactionType.Deposit,
            Status = (int)EscrowTransactionStatus.Succeeded, PaymentGateway = "InternalTokenWallet",
            GatewayTransactionCode = transactionCode, Note = "Contract amendment budget increase.",
            CreatedAt = clock.UtcNow, CompletedAt = clock.UtcNow
        });

        await ApplyAsync(context, clock, contract, amendment, cancellationToken);
    }
}
