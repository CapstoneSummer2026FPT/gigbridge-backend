using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260803120000_OptimizeDeliveryOutboxDispatch")]
public partial class OptimizeDeliveryOutboxDispatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DeliveryOutboxes_Status_NextAttemptAt",
            table: "DeliveryOutboxes");

        migrationBuilder.AddColumn<Guid>(
            name: "ClaimToken",
            table: "DeliveryOutboxes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DeliveryOutboxMaintenanceStates",
            columns: table => new
            {
                Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                WindowStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryOutboxMaintenanceStates", x => x.Operation);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryOutboxes_Active_Channel_Status_Due_Id",
            table: "DeliveryOutboxes",
            columns: new[] { "Channel", "Status", "NextAttemptAt", "DeliveryOutboxId" },
            filter: "\"Status\" IN (0, 1)");

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryOutboxes_Delivered_Retention",
            table: "DeliveryOutboxes",
            columns: new[] { "Status", "DeliveredAt" },
            filter: "\"Status\" = 2 AND \"DeliveredAt\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DeliveryOutboxMaintenanceStates");

        migrationBuilder.DropIndex(
            name: "IX_DeliveryOutboxes_Active_Channel_Status_Due_Id",
            table: "DeliveryOutboxes");

        migrationBuilder.DropIndex(
            name: "IX_DeliveryOutboxes_Delivered_Retention",
            table: "DeliveryOutboxes");

        migrationBuilder.DropColumn(
            name: "ClaimToken",
            table: "DeliveryOutboxes");

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryOutboxes_Status_NextAttemptAt",
            table: "DeliveryOutboxes",
            columns: new[] { "Status", "NextAttemptAt" });
    }
}
