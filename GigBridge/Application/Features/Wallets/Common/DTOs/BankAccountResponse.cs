namespace Application.Features.Wallets.Common.DTOs;

public sealed record BankAccountResponse(
    Guid BankAccountId,
    Guid UserId,
    string? BankBin,
    string BankCode,
    string BankName,
    string AccountNumberMasked,
    string AccountName,
    int Status,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static BankAccountResponse FromEntity(Domain.Entities.BankAccount bankAccount)
    {
        return new BankAccountResponse(
            bankAccount.BankAccountId,
            bankAccount.UserId,
            bankAccount.BankBin,
            bankAccount.BankCode,
            bankAccount.BankName,
            bankAccount.AccountNumberMasked,
            bankAccount.AccountName,
            bankAccount.Status,
            bankAccount.IsDefault,
            bankAccount.CreatedAt,
            bankAccount.UpdatedAt);
    }
}
