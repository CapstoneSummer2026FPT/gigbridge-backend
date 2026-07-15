namespace Application.Features.Wallets.Common.DTOs;

public sealed record SupportedBankResponse(
    string Bin,
    string Code,
    string ShortName,
    string Name,
    string? Logo);
