using Application.Common.Interfaces;
using Application.Features.Admin.Reconciliation.Common.DTOs;
using Application.Features.Admin.Reconciliation.Common.Internal;
using Domain.Entities;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Admin.Reconciliation.Queries;

public sealed class GetEscrowReconciliationReportQueryHandler :
    IRequestHandler<GetEscrowReconciliationReportQuery, EscrowReconciliationReport>
{
    private readonly IApplicationDbContext _context;

    public GetEscrowReconciliationReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EscrowReconciliationReport> Handle(
        GetEscrowReconciliationReportQuery request,
        CancellationToken cancellationToken)
    {
        // Read-only report: no tracking needed and the query never mutates state.
        var escrowRows = await _context.Set<ContractEscrow>()
            .Select(escrow => new
            {
                escrow.ContractEscrowId,
                escrow.ContractsId,
                escrow.Contract.Title,
                escrow.Status,
                escrow.RequiredAmount,
                escrow.FundedAmount,
                escrow.DepositedTokens,
                escrow.EarnedTokens,
                escrow.ReleasedAmount
            })
            .ToListAsync(cancellationToken);

        var contractRows = await _context.Set<Contract>()
            .Where(contract => contract.Milestones.Any())
            .Select(contract => new
            {
                contract.ContractsId,
                contract.Title,
                contract.TotalBudget,
                MilestoneTotal = contract.Milestones.Sum(milestone => (decimal?)milestone.Amount) ?? 0m,
                MilestoneCount = contract.Milestones.Count()
            })
            .ToListAsync(cancellationToken);

        var wallets = await _context.Set<UserWallet>()
            .ToListAsync(cancellationToken);

        var ledger = await _context.Set<WalletTransaction>()
            .Where(transaction => transaction.Status == (int)WalletTransactionStatus.Succeeded)
            .Select(transaction => new
            {
                transaction.UserId,
                transaction.Type,
                transaction.TokenAmount,
                transaction.BalanceSource,
                transaction.DepositedAmount,
                transaction.EarnedAmount,
                transaction.IdempotencyKey
            })
            .ToListAsync(cancellationToken);

        var rows = ledger
            .Select(item => new WalletTransactionRow(
                item.UserId,
                item.Type,
                item.TokenAmount,
                item.BalanceSource,
                item.DepositedAmount,
                item.EarnedAmount,
                item.IdempotencyKey))
            .ToList();

        var fundingDrift = escrowRows
            .Where(row => row.FundedAmount != 0m &&
                          Math.Abs(row.FundedAmount - row.RequiredAmount) > ReconciliationLedger.Tolerance)
            .Select(row => new EscrowFundingDriftItem(
                row.ContractEscrowId,
                row.ContractsId,
                row.Title,
                row.Status,
                row.RequiredAmount,
                row.FundedAmount,
                row.FundedAmount - row.RequiredAmount,
                row.RequiredAmount != 0m && row.FundedAmount * 1000m == row.RequiredAmount))
            .OrderBy(item => item.ContractTitle)
            .ToList();

        var compositionDrift = escrowRows
            .Select(row => new
            {
                Row = row,
                Composition = row.DepositedTokens + row.EarnedTokens + row.ReleasedAmount
            })
            .Where(item => Math.Abs(item.Composition - item.Row.FundedAmount) > ReconciliationLedger.Tolerance)
            .Select(item => new EscrowCompositionDriftItem(
                item.Row.ContractEscrowId,
                item.Row.ContractsId,
                item.Row.Title,
                item.Row.DepositedTokens,
                item.Row.EarnedTokens,
                item.Row.ReleasedAmount,
                item.Row.FundedAmount,
                item.Composition - item.Row.FundedAmount,
                item.Row.FundedAmount != 0m && item.Composition * 1000m == item.Row.FundedAmount))
            .OrderBy(item => item.ContractTitle)
            .ToList();

        var milestoneDrift = contractRows
            .Where(row => Math.Abs(row.TotalBudget - row.MilestoneTotal) > ReconciliationLedger.Tolerance)
            .Select(row => new MilestonePlanDriftItem(
                row.ContractsId,
                row.Title,
                row.TotalBudget,
                row.MilestoneTotal,
                row.MilestoneCount,
                row.TotalBudget - row.MilestoneTotal))
            .OrderBy(item => item.ContractTitle)
            .ToList();

        var walletRows = rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate(PoolDelta.Zero, (acc, row) => Add(acc, ReconciliationLedger.DeltaFor(row))));

        var unclassifiedCount = 0;
        foreach (var row in rows)
        {
            if (row.Type == (int)WalletTransactionType.Adjustment &&
                ReconciliationLedger.ClassifyAdjustment(row.IdempotencyKey) == AdjustmentKind.Unclassified)
            {
                unclassifiedCount++;
            }
        }

        var walletDrift = wallets
            .Select(wallet => new
            {
                Wallet = wallet,
                Expected = walletRows.GetValueOrDefault(wallet.UserId)
            })
            .Select(item => new WalletPoolDriftItem(
                item.Wallet.UserId,
                item.Wallet.AvailableTokens,
                item.Expected.Available,
                item.Wallet.WithdrawableTokens,
                item.Expected.Withdrawable,
                item.Wallet.HeldTokens,
                item.Expected.Held,
                item.Wallet.PendingWithdrawalTokens,
                item.Expected.Pending,
                CountUnclassifiedAdjustments(rows, item.Wallet.UserId)))
            .Where(item =>
                ReconciliationLedger.Drifts(item.AvailableTokens, item.ExpectedAvailable) ||
                ReconciliationLedger.Drifts(item.WithdrawableTokens, item.ExpectedWithdrawable) ||
                ReconciliationLedger.Drifts(item.HeldTokens, item.ExpectedHeld) ||
                ReconciliationLedger.Drifts(item.PendingWithdrawalTokens, item.ExpectedPending))
            .OrderBy(item => item.UserId)
            .ToList();

        var summary = new ReconciliationSummary(
            escrowRows.Count,
            contractRows.Count,
            wallets.Count,
            fundingDrift.Count,
            compositionDrift.Count,
            milestoneDrift.Count,
            walletDrift.Count,
            unclassifiedCount);

        return new EscrowReconciliationReport(
            fundingDrift,
            compositionDrift,
            milestoneDrift,
            walletDrift,
            summary);
    }

    private static PoolDelta Add(PoolDelta left, PoolDelta right) =>
        new(
            left.Available + right.Available,
            left.Withdrawable + right.Withdrawable,
            left.Held + right.Held,
            left.Pending + right.Pending);

    private static int CountUnclassifiedAdjustments(IReadOnlyList<WalletTransactionRow> rows, Guid userId) =>
        rows.Count(row =>
            row.UserId == userId &&
            row.Type == (int)WalletTransactionType.Adjustment &&
            ReconciliationLedger.ClassifyAdjustment(row.IdempotencyKey) == AdjustmentKind.Unclassified);
}
