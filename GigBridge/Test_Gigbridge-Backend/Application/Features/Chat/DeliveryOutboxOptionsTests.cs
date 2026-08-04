using Application;
using Application.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Test_Gigbridge_backend.Application.Features.Chat;

public sealed class DeliveryOutboxOptionsTests
{
    [Fact]
    public void Validator_AcceptsDefaults()
    {
        var result = new DeliveryOutboxOptionsValidator().Validate(
            Options.DefaultName,
            new DeliveryOutboxOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_ReportsEveryInvalidBound()
    {
        var options = new DeliveryOutboxOptions
        {
            RealtimePollMilliseconds = 49,
            EmailPollMilliseconds = 60_001,
            BatchSize = 0
        };

        var result = new DeliveryOutboxOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.StartsWith("RealtimePollMilliseconds=49"));
        Assert.Contains(result.Failures, failure => failure.StartsWith("EmailPollMilliseconds=60001"));
        Assert.Contains(result.Failures, failure => failure.StartsWith("BatchSize=0"));
    }

    [Fact]
    public void Validator_RejectsOutboxConcurrencyAboveConnectionBudget()
    {
        var options = new DeliveryOutboxOptions
        {
            MaxConcurrentDbConnections = 11
        };

        var result = new DeliveryOutboxOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("outbox budget of 10"));
    }

    [Fact]
    public void Registration_BindsValidConfigurationWithoutClamping()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{DeliveryOutboxOptions.SectionName}:RealtimePollMilliseconds"] = "250",
            [$"{DeliveryOutboxOptions.SectionName}:ScheduleStartBackfillEnabled"] = "false",
            [$"{DeliveryOutboxOptions.SectionName}:MaxConcurrentDbConnections"] = "2"
        });

        var options = provider.GetRequiredService<IOptions<DeliveryOutboxOptions>>().Value;

        Assert.Equal(250, options.RealtimePollMilliseconds);
        Assert.False(options.ScheduleStartBackfillEnabled);
        Assert.Equal(2, options.MaxConcurrentDbConnections);
    }

    [Fact]
    public void Registration_RejectsOutOfRangeConfigurationInsteadOfClamping()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{DeliveryOutboxOptions.SectionName}:BatchSize"] = "500"
        });

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<DeliveryOutboxOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.StartsWith("BatchSize=500"));
    }

    [Fact]
    public void Registration_RejectsUnknownConfigurationKeys()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{DeliveryOutboxOptions.SectionName}:BatchSze"] = "25"
        });

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptions<DeliveryOutboxOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(
        IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        return services.BuildServiceProvider();
    }
}
