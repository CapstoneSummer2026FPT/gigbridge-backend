namespace Application.Features.Wallets.Common.Interfaces;

public interface IBankAccountProtector
{
    string Protect(string accountNumber);

    string Unprotect(string protectedAccountNumber);
}
