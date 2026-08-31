using System.IdentityModel.Tokens.Jwt;
using Domain.Entities;
using Domain.Enums.Accounts;
using Infrastructure.Adapters.Security.Auth;
using Microsoft.Extensions.Configuration;

namespace Test_Gigbridge_Backend.Infrastructure.Adapters.Security.Auth;

public sealed class JwtServiceTests
{
    private const int AccessTokenMinutes = 60;
    private const int RefreshTokenMinutes = 10_080;

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Freelancer)]
    public void GenerateToken_UsesSameConfiguredLifetime_ForEveryRole(UserRole role)
    {
        var service = new JwtService(CreateConfiguration());
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = $"{role.ToString().ToLowerInvariant()}@example.com",
            FullName = role.ToString(),
            Role = (int)role
        };

        var encodedToken = service.GenerateToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encodedToken);

        Assert.NotEqual(DateTime.MinValue, token.Payload.IssuedAt);
        Assert.NotNull(token.Payload.Expiration);
        Assert.Equal(
            TimeSpan.FromMinutes(AccessTokenMinutes),
            token.ValidTo - token.Payload.IssuedAt);
        Assert.Equal(RefreshTokenMinutes, service.GetRefreshTokenExpiryMinutes());
    }

    [Theory]
    [InlineData("AccessTokenMinutes", null)]
    [InlineData("AccessTokenMinutes", "0")]
    [InlineData("RefreshTokenMinutes", null)]
    [InlineData("RefreshTokenMinutes", "invalid")]
    public void Constructor_RejectsMissingOrInvalidTokenLifetime(
        string setting,
        string? value)
    {
        var values = ValidJwtSettings();
        values[$"Jwt:{setting}"] = value;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => new JwtService(configuration));

        Assert.Contains($"Jwt:{setting}", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(ValidJwtSettings())
        .Build();

    private static Dictionary<string, string?> ValidJwtSettings() => new()
    {
        ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-bytes-long",
        ["Jwt:Issuer"] = "GigBridgeTests",
        ["Jwt:Audience"] = "GigBridgeTestUsers",
        ["Jwt:AccessTokenMinutes"] = AccessTokenMinutes.ToString(),
        ["Jwt:RefreshTokenMinutes"] = RefreshTokenMinutes.ToString()
    };
}
