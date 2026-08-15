using Application.Common.InternalServices.Wallets.Models;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Application.Features.Wallets.Common.Withdrawals;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.Withdrawals.Sync;

public sealed class SyncWithdrawalCommandHandler :
    IRequestHandler<SyncWithdrawalCommand, WithdrawalResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IPayoutProvider _payoutProvider;

    public SyncWithdrawalCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IPayoutProvider payoutProvider)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _payoutProvider = payoutProvider;
    }

    public async Task<WithdrawalResponse> Handle(
        SyncWithdrawalCommand command,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<WalletWithdrawal>().AsNoTracking().AsQueryable();
        if (!command.IsAdmin)
        {
            query = query.Where(withdrawal => withdrawal.UserId == command.UserId);
        }

        var withdrawal = await query.FirstOrDefaultAsync(
            withdrawal => withdrawal.WalletWithdrawalId == command.WithdrawalId,
            cancellationToken);

        if (withdrawal is null)
        {
            throw new NotFoundException("Withdrawal does not exist.");
        }

        if (!WithdrawalWorkflow.IsTerminal(withdrawal.Status))
        {
            var status = await _payoutProvider.GetPayoutStatusAsync(
                new PayoutStatusRequest(
                    withdrawal.WalletWithdrawalId,
                    withdrawal.ProviderOrderCode,
                    withdrawal.ProviderPayoutId),
                cancellationToken);

            withdrawal = await WithdrawalWorkflow.ApplyProviderResultAsync(
                _context,
                _dateTimeService,
                withdrawal.WalletWithdrawalId,
                status,
                cancellationToken);
        }

        return WithdrawalResponse.FromEntity(withdrawal);
    }
}
