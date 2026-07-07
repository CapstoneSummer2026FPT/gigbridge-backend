using Application.Common.Exceptions;

namespace Application.Features.Wallets.Common.BankAccounts;

internal static class BankAccountWorkflow
{
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
