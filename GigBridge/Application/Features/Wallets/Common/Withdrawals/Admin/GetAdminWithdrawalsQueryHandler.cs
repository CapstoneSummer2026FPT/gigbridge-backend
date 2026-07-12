using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed class GetAdminWithdrawalsQueryHandler :
    IRequestHandler<GetAdminWithdrawalsQuery, IReadOnlyList<WithdrawalResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminWithdrawalsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WithdrawalResponse>> Handle(
        GetAdminWithdrawalsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 200);
        var query = _context.Set<WalletWithdrawal>().AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(withdrawal => withdrawal.Status == request.Status.Value);
        }

        var withdrawals = await query
            .OrderByDescending(withdrawal => withdrawal.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return withdrawals.Select(WithdrawalResponse.FromEntity).ToList();
    }
}
