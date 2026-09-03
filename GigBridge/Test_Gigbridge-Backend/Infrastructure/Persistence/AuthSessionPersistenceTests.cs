using Infrastructure.Persistence;
using Infrastructure.Persistence.HealthChecks;
using Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Test_Gigbridge_Backend.Infrastructure.Persistence;

public sealed class AuthSessionPersistenceTests
{
    [Fact]
    public void MigrationCreatesAuthSessionsAndBackfillsLegacyUserTokens()
    {
        var migration = new AddAuthSessions();

        var createTable = Assert.Single(
            migration.UpOperations.OfType<CreateTableOperation>(),
            operation => operation.Name == "AuthSessions");
        Assert.Contains(createTable.Columns, column => column.Name == "RefreshTokenHash");
        Assert.Contains(
            migration.UpOperations.OfType<SqlOperation>(),
            operation =>
                operation.Sql.Contains("INSERT INTO \"AuthSessions\"", StringComparison.Ordinal) &&
                operation.Sql.Contains("FROM \"Users\"", StringComparison.Ordinal));
        Assert.DoesNotContain(
            migration.UpOperations.OfType<DropColumnOperation>(),
            operation => operation.Table == "Users");
    }

    [Fact]
    public async Task SchemaHealthCheckIsHealthyWhenAuthSessionsTableIsAvailable()
    {
        var databaseName = $"auth-session-health-{Guid.NewGuid():N}";
        var dbOptions = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using var context = new GigbridgeDbContext(dbOptions);
        var healthCheck = new AuthSessionSchemaHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
