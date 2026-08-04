namespace Application.Common.Interfaces.IService;

public interface ISupportedBankDirectory
{
    Task<IReadOnlyList<SupportedBank>> GetBanksAsync(CancellationToken cancellationToken);
}

public sealed record SupportedBank(
    string Bin,
    string Code,
    string ShortName,
    string Name,
    string? Logo);
