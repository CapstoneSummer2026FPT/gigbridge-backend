using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260814090000_AddProjectReceiptNotificationRetryState")]
public partial class AddProjectReceiptNotificationRetryState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DeliveryLeaseExpiresAt",
            table: "ProjectReceipts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "NotificationAttemptCount",
            table: "ProjectReceipts",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "NotificationLastError",
            table: "ProjectReceipts",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextNotificationAttemptAt",
            table: "ProjectReceipts",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "now()");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReceipts_GenerationStatus_DeliveryLeaseExpiresAt",
            table: "ProjectReceipts",
            columns: new[] { "GenerationStatus", "DeliveryLeaseExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectReceipts_NotificationId_NextNotificationAttemptAt",
            table: "ProjectReceipts",
            columns: new[] { "NotificationId", "NextNotificationAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProjectReceipts_GenerationStatus_DeliveryLeaseExpiresAt",
            table: "ProjectReceipts");

        migrationBuilder.DropIndex(
            name: "IX_ProjectReceipts_NotificationId_NextNotificationAttemptAt",
            table: "ProjectReceipts");

        migrationBuilder.DropColumn(name: "DeliveryLeaseExpiresAt", table: "ProjectReceipts");
        migrationBuilder.DropColumn(name: "NotificationAttemptCount", table: "ProjectReceipts");
        migrationBuilder.DropColumn(name: "NotificationLastError", table: "ProjectReceipts");
        migrationBuilder.DropColumn(name: "NextNotificationAttemptAt", table: "ProjectReceipts");
    }
}
