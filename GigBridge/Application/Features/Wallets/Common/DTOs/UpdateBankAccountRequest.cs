namespace Application.Features.Wallets.Common.DTOs;

public sealed record UpdateBankAccountRequest(
    string? BankCode,
    string? BankName,
    string? AccountNumber,
    string? AccountName,
    bool? IsDefault);
