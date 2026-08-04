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

        var totalWithdrawn = await transactions
            .Where(transaction => transaction.Status == Succeeded &&
                                  transaction.Type == (int)WalletTransactionType.WithdrawalSuccess)
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
