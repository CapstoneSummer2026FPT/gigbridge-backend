using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.BankAccounts.Delete;

public sealed class DeleteBankAccountCommandHandler : IRequestHandler<DeleteBankAccountCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public DeleteBankAccountCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task Handle(DeleteBankAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await _context.Set<BankAccount>()
            .FirstOrDefaultAsync(
                account =>
                    account.BankAccountId == command.BankAccountId &&
                    account.UserId == command.UserId &&
                    account.DeletedAt == null,
                cancellationToken);

        if (account is null)
        {
            throw new NotFoundException("Bank account does not exist.");
        }

        var hasPendingWithdrawal = await _context.Set<WalletWithdrawal>()
            .AnyAsync(
                withdrawal =>
                    withdrawal.BankAccountId == account.BankAccountId &&
                    (withdrawal.Status == (int)WithdrawalStatus.Pending ||
                        withdrawal.Status == (int)WithdrawalStatus.Processing ||
                        withdrawal.Status == (int)WithdrawalStatus.SyncRequired),
                cancellationToken);

        if (hasPendingWithdrawal)
        {
            throw new ConflictException("Bank account cannot be deleted while a withdrawal is pending.");
        }

        var now = _dateTimeService.UtcNow;
        var wasDefault = account.IsDefault;
        account.IsDefault = false;
        account.Status = (int)BankAccountStatus.Disabled;
        account.DeletedAt = now;
        account.UpdatedAt = now;

        if (wasDefault)
        {
            var nextDefault = await _context.Set<BankAccount>()
                .Where(candidate =>
                    candidate.UserId == command.UserId &&
                    candidate.BankAccountId != account.BankAccountId &&
                    candidate.DeletedAt == null &&
                    candidate.Status == (int)BankAccountStatus.Active)
                .OrderByDescending(candidate => candidate.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextDefault is not null)
            {
                nextDefault.IsDefault = true;
                nextDefault.UpdatedAt = now;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
