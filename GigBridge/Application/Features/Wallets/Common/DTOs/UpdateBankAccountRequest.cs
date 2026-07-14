namespace Application.Features.Wallets.Common.DTOs;

public sealed record UpdateBankAccountRequest(
    string? BankBin,
    string? BankCode,
    string? BankName,
    string? AccountNumber,
    string? AccountName,
    bool? IsDefault);
