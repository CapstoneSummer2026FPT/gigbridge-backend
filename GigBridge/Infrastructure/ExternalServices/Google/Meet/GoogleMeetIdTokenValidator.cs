using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Google.Meet;

internal sealed record ValidatedGoogleMeetIdToken(string Subject, string Email);

internal sealed class GoogleMeetIdTokenValidator
{
    private readonly GoogleMeetOptions _options;

    public GoogleMeetIdTokenValidator(IOptions<GoogleMeetOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ValidatedGoogleMeetIdToken?> ValidateAsync(
        string? idToken,
        string expectedNonceHash)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId]
                });

            if (string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email)
                || payload.EmailVerified != true
                || !HasExpectedNonce(payload.Nonce, expectedNonceHash))
            {
                return null;
            }

            return new ValidatedGoogleMeetIdToken(payload.Subject, payload.Email);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }

    private static bool HasExpectedNonce(string? nonce, string expectedNonceHash)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedNonceHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(nonce));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
