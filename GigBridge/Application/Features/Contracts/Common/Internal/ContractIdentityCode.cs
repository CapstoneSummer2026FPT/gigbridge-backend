using Application.Common.Exceptions;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractIdentityCode
{
    private const string DuplicatePartiesMessage =
        "The client and freelancer must use different identity or tax codes.";

    public static string Normalize(string? value)
    {
        var normalized = string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
        if ((normalized.Length != 9 && normalized.Length != 12) || !normalized.All(IsAsciiDigit))
        {
            throw new BadRequestException("Identity code must contain exactly 9 or 12 digits.");
        }

        return normalized;
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && IsNormalizedValueValid(
            string.Concat(value.Where(character => !char.IsWhiteSpace(character))));

    public static string? FirstValid(params string?[] values)
    {
        foreach (var value in values)
        {
            if (IsValid(value))
            {
                return Normalize(value);
            }
        }

        return null;
    }

    public static void EnsureDifferentParties(
        string identityOrTaxCode,
        params string?[] counterpartCandidates)
    {
        var counterpartIdentityOrTaxCode = FirstValid(counterpartCandidates);
        if (counterpartIdentityOrTaxCode is not null &&
            string.Equals(
                Normalize(identityOrTaxCode),
                counterpartIdentityOrTaxCode,
                StringComparison.Ordinal))
        {
            throw new BadRequestException(DuplicatePartiesMessage);
        }
    }

    private static bool IsNormalizedValueValid(string value) =>
        (value.Length == 9 || value.Length == 12) && value.All(IsAsciiDigit);

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
