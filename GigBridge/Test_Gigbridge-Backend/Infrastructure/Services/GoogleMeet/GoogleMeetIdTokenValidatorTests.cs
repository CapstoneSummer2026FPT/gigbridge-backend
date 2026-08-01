using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.ExternalServices.GoogleMeet;
using Infrastructure.Services.GoogleMeet;
using Microsoft.Extensions.Options;

namespace Test_Gigbridge_Backend.Infrastructure.Services.GoogleMeet;

public class GoogleMeetIdTokenValidatorTests
{
    [Fact]
    public async Task ValidateAsync_RejectsUnsignedTokenEvenWhenClaimsAndNonceAreValid()
    {
        // Arrange
        const string clientId = "google-client-id";
        const string nonce = "expected-nonce";
        var token = CreateUnsignedToken(clientId, nonce);
        var expectedNonceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));
        var validator = new GoogleMeetIdTokenValidator(
            Options.Create(new GoogleMeetOptions { ClientId = clientId }));

        // Act
        var result = await validator.ValidateAsync(token, expectedNonceHash);

        // Assert
        Assert.Null(result);
    }

    private static string CreateUnsignedToken(string audience, string nonce)
    {
        var header = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            alg = "none",
            typ = "JWT"
        }));
        var payload = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            iss = "https://accounts.google.com",
            sub = "google-subject",
            aud = audience,
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            nonce,
            email = "verified@example.com",
            email_verified = true
        }));

        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
