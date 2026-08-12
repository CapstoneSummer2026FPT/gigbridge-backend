using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

public sealed class GetWalletTransactionsSummaryQueryHandler :
    IRequestHandler<GetWalletTransactionsSummaryQuery, WalletTransactionsSummaryResponse>
{
    private const int Succeeded = (int)WalletTransactionStatus.Succeeded;
    private const int Pending = (int)WalletTransactionStatus.Pending;

    private readonly IApplicationDbContext _context;

    public GetWalletTransactionsSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WalletTransactionsSummaryResponse> Handle(
        GetWalletTransactionsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var transactions = _context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.UserId == request.UserId);

        var totalDeposits = await transactions
            .Where(transaction => transaction.Status == Succeeded &&
                                  transaction.Type == (int)WalletTransactionType.TopUp)
            .SumAsync(transaction => transaction.TokenAmount, cancellationToken);

        var totalEscrow = await transactions
            .Where(transaction => transaction.Status == Succeeded &&
                                  transaction.Type == (int)WalletTransactionType.EscrowHold)
            .SumAsync(transaction => transaction.TokenAmount, cancellationToken);

        var totalRefunds = await transactions
            .Where(transaction => transaction.Status == Succeeded &&
                                  (transaction.Type == (int)WalletTransactionType.EscrowRefund ||
                                   transaction.Type == (int)WalletTransactionType.WithdrawalRefund))
            .SumAsync(transaction => transaction.TokenAmount, cancellationToken);

        // "Total Withdrawn" covers both a literal bank/gateway cash-out (WithdrawalSuccess) and
        // an escrow release credited to this user (early milestone withdrawal or a dispute-resolution
        // payout) — both move funds out of escrow into this user's own spendable wallet balance.
        // The paired debit-side EscrowRelease row (the client's side of the same release) is
        // excluded via the BalanceSource.Earned check, which only the credited party's row carries.
        var totalWithdrawn = await transactions
            .Where(transaction => transaction.Status == Succeeded &&
                                  (transaction.Type == (int)WalletTransactionType.WithdrawalSuccess ||
                                   (transaction.Type == (int)WalletTransactionType.EscrowRelease &&
                                    transaction.BalanceSource == (int)WalletBalanceSource.Earned)))
            .SumAsync(transaction => transaction.TokenAmount, cancellationToken);

        var pendingCount = await transactions
            .CountAsync(transaction => transaction.Status == Pending, cancellationToken);

        var totalTransactions = await transactions.CountAsync(cancellationToken);

        return new WalletTransactionsSummaryResponse(
            totalDeposits,
            totalEscrow,
            totalRefunds,
            totalWithdrawn,
            pendingCount,
            totalTransactions);
    }
}
