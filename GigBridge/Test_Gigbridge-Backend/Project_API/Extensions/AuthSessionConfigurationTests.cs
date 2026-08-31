using Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Project_API.Extensions;

namespace Test_Gigbridge_Backend.Project_API.Extensions;

public sealed class AuthSessionConfigurationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void OptionsValidatorRejectsUnsafeSessionLimit(int maximumSessions)
    {
        var result = new AuthSessionOptionsValidator().Validate(
            null,
            new AuthSessionOptions { MaxActiveSessionsPerUser = maximumSessions });

        Assert.True(result.Failed);
    }

    [Fact]
    public void ProductionRejectsMissingEnabledFlag()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.ValidateAuthSessionConfiguration(CreateEnvironment("Production")));

        Assert.Contains("AuthSessions:Enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAcceptsExplicitlyEnabledFlag()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthSessions:Enabled"] = "true"
            })
            .Build();

        configuration.ValidateAuthSessionConfiguration(CreateEnvironment("Production"));
    }

    [Fact]
    public void ProductionRejectsExplicitlyDisabledFlag()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthSessions:Enabled"] = "false"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.ValidateAuthSessionConfiguration(CreateEnvironment("Production")));

        Assert.Contains("configured explicitly as 'true'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnyEnvironmentRejectsInvalidEnabledFlag()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthSessions:Enabled"] = "sometimes"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            configuration.ValidateAuthSessionConfiguration(CreateEnvironment("Development")));
    }

    private static IHostEnvironment CreateEnvironment(string environmentName) =>
        new TestHostEnvironment { EnvironmentName = environmentName };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = null!;
        public string ApplicationName { get; set; } = "GigBridgeTests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
