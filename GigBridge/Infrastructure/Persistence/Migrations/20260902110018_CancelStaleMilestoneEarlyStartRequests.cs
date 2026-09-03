using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CancelStaleMilestoneEarlyStartRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "MilestoneEarlyStartRequests" AS request
                SET "Status" = 3,
                    "ResponseNote" = 'Automatically cancelled because the milestone started through the normal workflow.',
                    "RespondedByUserId" = NULL,
                    "RespondedAt" = NOW()
                FROM "Milestones" AS milestone
                WHERE request."MilestonesId" = milestone."MilestonesId"
                  AND request."Status" = 0
                  AND milestone."Status" <> 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair is intentionally irreversible: reverting these rows to Pending could
            // recreate requests that are invalid for their milestone's current state.
        }
    }
}
