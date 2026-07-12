using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.BankAccounts.Update;

public sealed class UpdateBankAccountCommandHandler :
    IRequestHandler<UpdateBankAccountCommand, BankAccountResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IBankAccountProtector _bankAccountProtector;

    public UpdateBankAccountCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IBankAccountProtector bankAccountProtector)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _bankAccountProtector = bankAccountProtector;
    }

    public async Task<BankAccountResponse> Handle(
        UpdateBankAccountCommand command,
        CancellationToken cancellationToken)
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

        if (hasPendingWithdrawal && !string.IsNullOrWhiteSpace(command.Request.AccountNumber))
        {
            throw new ConflictException("Bank account number cannot be changed while a withdrawal is pending.");
        }

        var now = _dateTimeService.UtcNow;

        if (!string.IsNullOrWhiteSpace(command.Request.BankCode))
        {
            account.BankCode = BankAccountWorkflow.NormalizeText(command.Request.BankCode, "Bank code", 30);
        }

        if (!string.IsNullOrWhiteSpace(command.Request.BankName))
        {
            account.BankName = BankAccountWorkflow.NormalizeText(command.Request.BankName, "Bank name", 120);
        }

        if (!string.IsNullOrWhiteSpace(command.Request.AccountName))
        {
            account.AccountName = BankAccountWorkflow.NormalizeText(command.Request.AccountName, "Account name", 120);
        }

        if (!string.IsNullOrWhiteSpace(command.Request.AccountNumber))
        {
            var normalized = BankAccountWorkflow.NormalizeAccountNumber(command.Request.AccountNumber);
            account.AccountNumberEncrypted = _bankAccountProtector.Protect(normalized);
            account.AccountNumberMasked = BankAccountWorkflow.MaskAccountNumber(normalized);
        }

        if (command.Request.IsDefault == true)
        {
            var existingDefaults = await _context.Set<BankAccount>()
                .Where(existing =>
                    existing.UserId == command.UserId &&
                    existing.BankAccountId != account.BankAccountId &&
                    existing.DeletedAt == null &&
                    existing.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
                existing.UpdatedAt = now;
            }

            account.IsDefault = true;
        }
        else if (command.Request.IsDefault == false)
        {
            account.IsDefault = false;
        }

        account.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return BankAccountResponse.FromEntity(account);
    }
}
