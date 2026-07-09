using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Get;

public sealed class GetWithdrawalsQueryHandler :
    IRequestHandler<GetWithdrawalsQuery, IReadOnlyList<WithdrawalResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetWithdrawalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WithdrawalResponse>> Handle(
        GetWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);
        var withdrawals = await _context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .Where(withdrawal => withdrawal.UserId == request.UserId)
            .OrderByDescending(withdrawal => withdrawal.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return withdrawals.Select(WithdrawalResponse.FromEntity).ToList();
    }
}
