using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.GetTransactions.Queries;

public sealed class GetWalletTransactionsQueryHandler :
    IRequestHandler<GetWalletTransactionsQuery, IReadOnlyList<WalletTransactionResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetWalletTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WalletTransactionResponse>> Handle(
        GetWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var transactions = await _context.Set<WalletTransaction>()
            .Where(transaction => transaction.UserId == request.UserId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return transactions
            .Select(WalletTransactionResponse.FromEntity)
            .ToList();
    }
}
