using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.GetDetail;

public sealed class GetWithdrawalDetailQueryHandler :
    IRequestHandler<GetWithdrawalDetailQuery, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;

    public GetWithdrawalDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WithdrawalResponse> Handle(
        GetWithdrawalDetailQuery request,
        CancellationToken cancellationToken)
    {
        var withdrawal = await _context.Set<WalletWithdrawal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                withdrawal =>
                    withdrawal.WalletWithdrawalId == request.WithdrawalId &&
                    withdrawal.UserId == request.UserId,
                cancellationToken);

        return withdrawal is null
            ? throw new NotFoundException("Withdrawal does not exist.")
            : WithdrawalResponse.FromEntity(withdrawal);
    }
}
