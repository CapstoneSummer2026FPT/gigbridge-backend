namespace Application.Common.Interfaces.IService;

public interface IBankAccountProtector
{
    string Protect(string accountNumber);

    string Unprotect(string protectedAccountNumber);
}
