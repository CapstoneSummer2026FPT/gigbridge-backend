using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;

namespace Application.Features.Wallets.Common.BankAccounts;

internal static class BankAccountWorkflow
{
    public static async Task<SupportedBank> ResolveBankAsync(
        ISupportedBankDirectory directory,
        string bankBin,
        string bankCode,
        string bankName,
        CancellationToken cancellationToken)
    {
        var normalizedBin = bankBin.Trim();
        if (normalizedBin.Length != 6 || normalizedBin.Any(character => !char.IsDigit(character)))
        {
            throw new BadRequestException("Bank BIN must contain exactly 6 digits.");
        }

        var banks = await directory.GetBanksAsync(cancellationToken);
        var matched = banks.FirstOrDefault(bank => bank.Bin == normalizedBin);
        if (banks.Count > 0 && matched is null)
        {
            throw new BadRequestException("Bank BIN is not supported.");
        }

        return matched ?? new SupportedBank(
            normalizedBin,
            NormalizeText(bankCode, "Bank code", 30).ToUpperInvariant(),
            NormalizeText(bankCode, "Bank code", 30).ToUpperInvariant(),
            NormalizeText(bankName, "Bank name", 120),
            null);
    }

    public static string NormalizeAccountNumber(string accountNumber)
    {
        var normalized = new string(
            accountNumber
                .Where(character => !char.IsWhiteSpace(character) && character != '-')
                .ToArray());

        if (normalized.Length < 4 || normalized.Length > 40)
        {
            throw new BadRequestException("Bank account number must be between 4 and 40 characters.");
        }

        if (normalized.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new BadRequestException("Bank account number can only contain letters and digits.");
        }

        return normalized.ToUpperInvariant();
    }

    public static string MaskAccountNumber(string normalizedAccountNumber)
    {
        var visibleLength = Math.Min(4, normalizedAccountNumber.Length);
        var visible = normalizedAccountNumber[^visibleLength..];
        return $"{new string('*', normalizedAccountNumber.Length - visibleLength)}{visible}";
    }

    public static string NormalizeText(string value, string fieldName, int maxLength)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BadRequestException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new BadRequestException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
