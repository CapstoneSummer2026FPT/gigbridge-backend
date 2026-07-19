using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Creates the EF context when commands are run directly from the Infrastructure project.
/// Runtime context creation remains owned by DependencyInjection.AddInfrastructureServices.
/// </summary>
public sealed class GigbridgeDbContextFactory : IDesignTimeDbContextFactory<GigbridgeDbContext>
{
    public GigbridgeDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = FindApiDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. Configure it in " +
                "Project_API/appsettings.json or ConnectionStrings__DefaultConnection.");

        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new GigbridgeDbContext(options);
    }

    private static string FindApiDirectory()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var directory = current; directory is not null; directory = directory.Parent)
        {
            var direct = Path.Combine(directory.FullName, "Project_API", "appsettings.json");
            if (File.Exists(direct)) return Path.GetDirectoryName(direct)!;

            var nested = Path.Combine(directory.FullName, "GigBridge", "Project_API", "appsettings.json");
            if (File.Exists(nested)) return Path.GetDirectoryName(nested)!;
        }

        throw new InvalidOperationException(
            "Could not locate Project_API/appsettings.json from the current directory.");
    }
}
