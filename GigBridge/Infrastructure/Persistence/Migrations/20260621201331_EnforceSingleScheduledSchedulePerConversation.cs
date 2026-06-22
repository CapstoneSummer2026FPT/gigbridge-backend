using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleScheduledSchedulePerConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Schedules"
                SET "Status" = 2,
                    "UpdatedAt" = NOW(),
                    "Version" = "Version" + 1
                WHERE "Status" = 0
                  AND "ScheduledAtUtc" <= NOW();

                WITH ranked AS (
                    SELECT "ScheduleId",
                           ROW_NUMBER() OVER (
                               PARTITION BY "ConversationId"
                               ORDER BY "ScheduledAtUtc", "CreatedAt", "ScheduleId") AS position
                    FROM "Schedules"
                    WHERE "Status" = 0
                )
                UPDATE "Schedules" AS schedule
                SET "Status" = 1,
                    "CancellationReason" = COALESCE(
                        schedule."CancellationReason",
                        'Automatically cancelled while enforcing one ongoing schedule per conversation.'),
                    "CancelledAt" = COALESCE(schedule."CancelledAt", NOW()),
                    "UpdatedAt" = NOW(),
                    "Version" = schedule."Version" + 1
                FROM ranked
                WHERE schedule."ScheduleId" = ranked."ScheduleId"
                  AND ranked.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Schedules_ConversationId_Scheduled",
                table: "Schedules",
                column: "ConversationId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Schedules_ConversationId_Scheduled",
                table: "Schedules");

            migrationBuilder.Sql(
                """
                UPDATE "Schedules"
                SET "Status" = 0,
                    "UpdatedAt" = NOW(),
                    "Version" = "Version" + 1
                WHERE "Status" = 2;
                """);
        }
    }
}
