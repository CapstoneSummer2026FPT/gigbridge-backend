using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Application.Common.Options;

public static class BackgroundWorkerOptions
{
    public const string SectionName = "BackgroundWorkers";

    public static bool IsEnabled(IConfiguration configuration)
    {
        var configuredValue = configuration[$"{SectionName}:Enabled"];
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            if (bool.TryParse(configuredValue, out var enabled))
            {
                return enabled;
            }

            throw new InvalidOperationException(
                $"{SectionName}:Enabled must be either 'true' or 'false'.");
        }

        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ??
                              configuration["DOTNET_ENVIRONMENT"] ??
                              Environments.Production;
        return string.Equals(
            environmentName,
            Environments.Production,
            StringComparison.OrdinalIgnoreCase);
    }
}
