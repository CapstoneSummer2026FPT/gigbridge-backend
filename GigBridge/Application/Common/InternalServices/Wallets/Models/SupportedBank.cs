namespace Application.Common.InternalServices.Wallets.Models;
public sealed record SupportedBank(
    string Bin,
    string Code,
    string ShortName,
    string Name,
    string? Logo);
