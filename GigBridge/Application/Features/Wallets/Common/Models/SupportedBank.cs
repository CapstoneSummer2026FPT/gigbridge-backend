namespace Application.Features.Wallets.Common.Models;

public sealed record SupportedBank(
    string Bin,
    string Code,
    string ShortName,
    string Name,
    string? Logo);
