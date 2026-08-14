using Application.Features.Wallets.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Adapters.Security.Wallets;

public sealed class BankAccountProtector : IBankAccountProtector
{
    private readonly IDataProtector _protector;

    public BankAccountProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("GigBridge.BankAccounts.v1");
    }

    public string Protect(string accountNumber)
    {
        return _protector.Protect(accountNumber);
    }

    public string Unprotect(string protectedAccountNumber)
    {
        return _protector.Unprotect(protectedAccountNumber);
    }
}
