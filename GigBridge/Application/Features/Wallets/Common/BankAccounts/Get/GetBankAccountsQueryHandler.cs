using Application.Common.Interfaces;
using Application.Common.Interfaces.Time;
using Application.Common.InternalServices.Wallets.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using Domain.Enums.Wallets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Application.Features.Wallets.Common.BankAccounts.Get;

public sealed class GetBankAccountsQueryHandler :
    IRequestHandler<GetBankAccountsQuery, IReadOnlyList<BankAccountResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IBankAccountProtector _bankAccountProtector;
    private readonly IDateTimeService _dateTimeService;

    public GetBankAccountsQueryHandler(
        IApplicationDbContext context,
        IBankAccountProtector bankAccountProtector,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _bankAccountProtector = bankAccountProtector;
        _dateTimeService = dateTimeService;
    }

    public async Task<IReadOnlyList<BankAccountResponse>> Handle(
        GetBankAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var accounts = await _context.Set<BankAccount>()
            .Where(account => account.UserId == request.UserId && account.DeletedAt == null)
            .OrderByDescending(account => account.IsDefault)
            .ThenByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var account in accounts.Where(account => account.Status == (int)BankAccountStatus.Active))
        {
            try
            {
                _bankAccountProtector.Unprotect(account.AccountNumberEncrypted);
            }
            catch (CryptographicException)
            {
                account.Status = (int)BankAccountStatus.Disabled;
                account.IsDefault = false;
                account.UpdatedAt = _dateTimeService.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return accounts.Select(BankAccountResponse.FromEntity).ToList();
    }
}
