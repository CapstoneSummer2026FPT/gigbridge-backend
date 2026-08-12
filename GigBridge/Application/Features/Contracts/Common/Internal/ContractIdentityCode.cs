using Application.Common.Exceptions;

namespace Application.Features.Contracts.Common.Internal;

internal static class ContractIdentityCode
{
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

    private static bool IsNormalizedValueValid(string value) =>
        (value.Length == 9 || value.Length == 12) && value.All(IsAsciiDigit);

    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
}
