using Application.Common.Interfaces;
using Application.Features.Wallets.Common.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Wallets.Common.BankAccounts.Get;

public sealed class GetBankAccountsQueryHandler :
    IRequestHandler<GetBankAccountsQuery, IReadOnlyList<BankAccountResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetBankAccountsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BankAccountResponse>> Handle(
        GetBankAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var accounts = await _context.Set<BankAccount>()
            .AsNoTracking()
            .Where(account => account.UserId == request.UserId && account.DeletedAt == null)
            .OrderByDescending(account => account.IsDefault)
            .ThenByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(BankAccountResponse.FromEntity).ToList();
    }
}
