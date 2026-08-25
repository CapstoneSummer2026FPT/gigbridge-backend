using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Application.Features.Wallets.Common.BankAccounts.Update;

public sealed class UpdateBankAccountCommandHandler :
    IRequestHandler<UpdateBankAccountCommand, BankAccountResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IBankAccountProtector _bankAccountProtector;
    private readonly ISupportedBankDirectory _bankDirectory;

    public UpdateBankAccountCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IBankAccountProtector bankAccountProtector,
        ISupportedBankDirectory bankDirectory)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _bankAccountProtector = bankAccountProtector;
        _bankDirectory = bankDirectory;
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

        var changesRouting = !string.IsNullOrWhiteSpace(command.Request.BankBin) ||
            !string.IsNullOrWhiteSpace(command.Request.AccountNumber);
        if (hasPendingWithdrawal && changesRouting)
        {
            throw new ConflictException("Bank routing details cannot be changed while a withdrawal is pending.");
        }

        var now = _dateTimeService.UtcNow;
        var accountNumberIsValid = true;
        try
        {
            _bankAccountProtector.Unprotect(account.AccountNumberEncrypted);
        }
        catch (CryptographicException)
        {
            accountNumberIsValid = false;
        }

        var changesBankDetails = !string.IsNullOrWhiteSpace(command.Request.BankBin) ||
            !string.IsNullOrWhiteSpace(command.Request.AccountNumber) ||
            !string.IsNullOrWhiteSpace(command.Request.AccountName);
        if (changesBankDetails)
        {
            var bank = await BankAccountWorkflow.ResolveBankAsync(
                _bankDirectory,
                command.Request.BankBin ?? account.BankBin ?? string.Empty,
                cancellationToken);
            account.BankBin = bank.Bin;
            account.BankCode = bank.Code;
            account.BankName = bank.Name;
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
            accountNumberIsValid = true;
        }

        var bankBinIsValid = account.BankBin?.Length == 6 && account.BankBin.All(char.IsDigit);
        account.Status = accountNumberIsValid && bankBinIsValid
            ? (int)BankAccountStatus.Active
            : (int)BankAccountStatus.Disabled;
        if (account.Status == (int)BankAccountStatus.Disabled)
        {
            account.IsDefault = false;
        }

        if (command.Request.IsDefault == true && account.Status == (int)BankAccountStatus.Disabled)
        {
            throw new BadRequestException("A disabled bank account cannot be set as default.");
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

        if (account.Status == (int)BankAccountStatus.Disabled)
        {
            account.IsDefault = false;
        }

        account.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        return BankAccountResponse.FromEntity(account);
    }
}
