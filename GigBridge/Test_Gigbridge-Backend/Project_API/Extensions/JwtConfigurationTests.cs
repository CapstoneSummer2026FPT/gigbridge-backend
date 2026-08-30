using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project_API.Extensions;

namespace Test_Gigbridge_Backend.Project_API.Extensions;

public sealed class JwtConfigurationTests
{
    [Theory]
    [InlineData("AccessTokenMinutes", null)]
    [InlineData("AccessTokenMinutes", "-1")]
    [InlineData("RefreshTokenMinutes", null)]
    [InlineData("RefreshTokenMinutes", "not-a-number")]
    public void AddJwtAuthentication_RejectsMissingOrInvalidTokenLifetime(
        string setting,
        string? value)
    {
        var values = ValidJwtSettings();
        values[$"Jwt:{setting}"] = value;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddJwtAuthentication(configuration));

        Assert.Contains($"Jwt:{setting}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddJwtAuthentication_AcceptsExplicitProductionLifetimes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ValidJwtSettings())
            .Build();
        var services = new ServiceCollection();

        services.AddJwtAuthentication(configuration);

        Assert.NotEmpty(services);
    }

    private static Dictionary<string, string?> ValidJwtSettings() => new()
    {
        ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-bytes-long",
        ["Jwt:Issuer"] = "GigBridgeTests",
        ["Jwt:Audience"] = "GigBridgeTestUsers",
        ["Jwt:AccessTokenMinutes"] = "60",
        ["Jwt:RefreshTokenMinutes"] = "10080"
    };
}
