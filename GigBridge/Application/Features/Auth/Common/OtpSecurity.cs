using System.Security.Cryptography;
using System.Text;

namespace Application.Features.Auth.Common;

internal enum OtpPurpose
{
    Signup,
    PasswordReset,
    IdentityVerification
}

internal static class OtpPurposeNames
{
    public const string Signup = "signup";
    public const string PasswordReset = "password_reset";
    public const string IdentityVerification = "identity_verification";

    public static bool TryParse(string? value, out OtpPurpose purpose)
    {
        purpose = value?.Trim().ToLowerInvariant() switch
        {
            Signup => OtpPurpose.Signup,
            PasswordReset => OtpPurpose.PasswordReset,
            IdentityVerification => OtpPurpose.IdentityVerification,
            _ => default
        };

        return value?.Trim().ToLowerInvariant() is Signup or PasswordReset or IdentityVerification;
    }
}

internal sealed record OtpChallengeState(
    string Otp,
    int FailedAttempts,
    DateTime ExpiresAtUtc);

internal static class OtpSecurity
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static string ChallengeKey(OtpPurpose purpose, string canonicalEmail) =>
        $"auth:otp:{ToSegment(purpose)}:challenge:{Hash(canonicalEmail)}";

    public static string CooldownKey(OtpPurpose purpose, string canonicalEmail) =>
        $"auth:otp:{ToSegment(purpose)}:cooldown:{Hash(canonicalEmail)}";

    public static string LockoutKey(OtpPurpose purpose, string canonicalEmail) =>
        $"auth:otp:{ToSegment(purpose)}:lockout:{Hash(canonicalEmail)}";

    public static string VerifiedKey(
        OtpPurpose purpose,
        string canonicalEmail,
        string proof,
        string? context = null) =>
        $"auth:otp:{ToSegment(purpose)}:verified:{Hash(canonicalEmail)}:{Hash(proof)}" +
        (context is null ? string.Empty : $":{Hash(context)}");

    public static bool Matches(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string ToSegment(OtpPurpose purpose) => purpose switch
    {
        OtpPurpose.Signup => OtpPurposeNames.Signup,
        OtpPurpose.PasswordReset => OtpPurposeNames.PasswordReset,
        OtpPurpose.IdentityVerification => OtpPurposeNames.IdentityVerification,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
