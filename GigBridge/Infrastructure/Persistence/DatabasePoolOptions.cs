using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Infrastructure.Persistence;

internal sealed class DatabasePoolOptions
{
    public const string SectionName = "DatabasePool";

    public int MaxPoolSize { get; set; } = 5;
    public int MinPoolSize { get; set; }
    public int ConnectionIdleLifetimeSeconds { get; set; } = 60;
    public int ConnectionPruningIntervalSeconds { get; set; } = 10;
    public string? ApplicationName { get; set; }

    public static string Apply(string connectionString, IConfiguration configuration)
    {
        var options = new DatabasePoolOptions();
        configuration.GetSection(SectionName).Bind(
            options,
            binder => binder.ErrorOnUnknownConfiguration = true);
        options.Validate();

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = options.MaxPoolSize,
            MinPoolSize = options.MinPoolSize,
            ConnectionIdleLifetime = options.ConnectionIdleLifetimeSeconds,
            ConnectionPruningInterval = options.ConnectionPruningIntervalSeconds
        };

        if (!string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            builder.ApplicationName = options.ApplicationName.Trim();
        }
        else if (string.IsNullOrWhiteSpace(builder.ApplicationName))
        {
            var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ??
                                  configuration["DOTNET_ENVIRONMENT"] ??
                                  "Unknown";
            builder.ApplicationName = $"GigBridge-{environmentName}";
        }

        return builder.ConnectionString;
    }

    private void Validate()
    {
        if (MaxPoolSize is < 1 or > 100)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaxPoolSize must be between 1 and 100.");
        }

        if (MinPoolSize < 0 || MinPoolSize > MaxPoolSize)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MinPoolSize must be between 0 and MaxPoolSize.");
        }

        if (ConnectionIdleLifetimeSeconds is < 10 or > 3_600)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ConnectionIdleLifetimeSeconds must be between 10 and 3600.");
        }

        if (ConnectionPruningIntervalSeconds < 1 ||
            ConnectionPruningIntervalSeconds > ConnectionIdleLifetimeSeconds)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ConnectionPruningIntervalSeconds must be between 1 and ConnectionIdleLifetimeSeconds.");
        }
    }
}
