using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.ExternalServices.Payments;

internal static class PayOsSignatureVerifier
{
    public static bool IsValid(
        IReadOnlyDictionary<string, string?> data,
        string? signature,
        string? checksumKey)
    {
        if (data.Count == 0 ||
            string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(checksumKey))
        {
            return false;
        }

        var signedData = string.Join(
            "&",
            data.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value ?? string.Empty}"));

        var computedSignature = ComputeHmacSha256(signedData, checksumKey);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    private static string ComputeHmacSha256(string data, string checksumKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
