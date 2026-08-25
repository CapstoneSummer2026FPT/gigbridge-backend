namespace Application.Features.Wallets.Common.DTOs;

public sealed record UpdateBankAccountRequest(
    string? BankBin,
    // Deprecated compatibility fields. Bank identity is resolved from BankBin.
    string? BankCode,
    string? BankName,
    string? AccountNumber,
    string? AccountName,
    bool? IsDefault);
