using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public class GigbridgeDbContextFactory : IDesignTimeDbContextFactory<GigbridgeDbContext>
{
    public GigbridgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GIGBRIDGE_DESIGNTIME_CONNECTION")
            ?? "Host=localhost;Database=gigbridge;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<GigbridgeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new GigbridgeDbContext(optionsBuilder.Options);
    }
}
