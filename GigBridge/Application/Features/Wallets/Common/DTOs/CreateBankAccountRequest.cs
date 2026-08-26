namespace Application.Features.Wallets.Common.DTOs;

public sealed record CreateBankAccountRequest(
    string BankBin,
    string AccountNumber,
    string AccountName,
    bool IsDefault = false,
    string? BankCode = null,
    string? BankName = null);
