using Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Test_Gigbridge_Backend.Infrastructure.Persistence;

public sealed class CancelStaleMilestoneEarlyStartRequestsMigrationTests
{
    [Fact]
    public void Up_CancelsOnlyPendingRequestsWhoseMilestoneIsNoLongerPending()
    {
        var operation = Assert.IsType<SqlOperation>(
            Assert.Single(new TestableMigration().BuildUpOperations()));

        Assert.Contains("SET \"Status\" = 3", operation.Sql, StringComparison.Ordinal);
        Assert.Contains("request.\"Status\" = 0", operation.Sql, StringComparison.Ordinal);
        Assert.Contains("milestone.\"Status\" <> 0", operation.Sql, StringComparison.Ordinal);
        Assert.Contains("\"RespondedByUserId\" = NULL", operation.Sql, StringComparison.Ordinal);
        Assert.Contains("\"RespondedAt\" = NOW()", operation.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Down_DoesNotRecreateInvalidPendingRequests()
    {
        Assert.Empty(new TestableMigration().BuildDownOperations());
    }

    private sealed class TestableMigration : CancelStaleMilestoneEarlyStartRequests
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Down(builder);
            return builder.Operations;
        }
    }
}
