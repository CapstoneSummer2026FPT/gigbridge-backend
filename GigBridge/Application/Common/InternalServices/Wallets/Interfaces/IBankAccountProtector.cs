namespace Application.Common.InternalServices.Wallets.Interfaces;
public interface IBankAccountProtector
{
    string Protect(string accountNumber);

    string Unprotect(string protectedAccountNumber);
}
