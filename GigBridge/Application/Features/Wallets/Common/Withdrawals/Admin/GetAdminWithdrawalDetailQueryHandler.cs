using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Admin;

public sealed class GetAdminWithdrawalDetailQueryHandler :
    IRequestHandler<GetAdminWithdrawalDetailQuery, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;

    public GetAdminWithdrawalDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WithdrawalResponse> Handle(
        GetAdminWithdrawalDetailQuery request,
        CancellationToken cancellationToken)
    {
        var withdrawal = await _context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                withdrawal => withdrawal.WalletWithdrawalId == request.WithdrawalId,
                cancellationToken);

        return withdrawal is null
            ? throw new NotFoundException("Withdrawal does not exist.")
            : WithdrawalResponse.FromEntity(withdrawal);
    }
}
