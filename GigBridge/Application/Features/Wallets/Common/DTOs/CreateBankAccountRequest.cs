namespace Application.Features.Wallets.Common.DTOs;

public sealed record CreateBankAccountRequest(
    string BankBin,
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountName,
    bool IsDefault = false);
