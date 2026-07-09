using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.BankAccounts.Create;

public sealed class CreateBankAccountCommandHandler :
    IRequestHandler<CreateBankAccountCommand, BankAccountResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IBankAccountProtector _bankAccountProtector;

    public CreateBankAccountCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IBankAccountProtector bankAccountProtector)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _bankAccountProtector = bankAccountProtector;
    }

    public async Task<BankAccountResponse> Handle(
        CreateBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == command.UserId, cancellationToken);

        if (user is null || user.Role != (int)UserRole.Freelancer)
        {
            throw new ForbiddenAccessException("Only freelancers can add payout bank accounts.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenAccessException("Inactive users cannot add payout bank accounts.");
        }

        var now = _dateTimeService.UtcNow;
        var normalizedAccountNumber = BankAccountWorkflow.NormalizeAccountNumber(command.Request.AccountNumber);
        var shouldSetDefault = command.Request.IsDefault ||
            !await _context.Set<BankAccount>().AnyAsync(
                account =>
                    account.UserId == command.UserId &&
                    account.DeletedAt == null &&
                    account.Status == (int)BankAccountStatus.Active,
                cancellationToken);

        if (shouldSetDefault)
        {
            var existingDefaults = await _context.Set<BankAccount>()
                .Where(account => account.UserId == command.UserId && account.DeletedAt == null && account.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var account in existingDefaults)
            {
                account.IsDefault = false;
                account.UpdatedAt = now;
            }
        }

        var bankAccount = new BankAccount
        {
            BankAccountId = Guid.NewGuid(),
            UserId = command.UserId,
            BankCode = BankAccountWorkflow.NormalizeText(command.Request.BankCode, "Bank code", 30),
            BankName = BankAccountWorkflow.NormalizeText(command.Request.BankName, "Bank name", 120),
            AccountNumberEncrypted = _bankAccountProtector.Protect(normalizedAccountNumber),
            AccountNumberMasked = BankAccountWorkflow.MaskAccountNumber(normalizedAccountNumber),
            AccountName = BankAccountWorkflow.NormalizeText(command.Request.AccountName, "Account name", 120),
            Status = (int)BankAccountStatus.Active,
            IsDefault = shouldSetDefault,
            CreatedAt = now
        };

        _context.Set<BankAccount>().Add(bankAccount);
        await _context.SaveChangesAsync(cancellationToken);

        return BankAccountResponse.FromEntity(bankAccount);
    }
}
