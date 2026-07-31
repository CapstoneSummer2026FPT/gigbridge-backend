using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260729120000_AddScheduleRescheduleRequests")]
public partial class AddScheduleRescheduleRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ProposedScheduledAtUtc",
            table: "Schedules",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProposedTimeZoneId",
            table: "Schedules",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RescheduleRequestCount",
            table: "Schedules",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProposedScheduledAtUtc",
            table: "Schedules");

        migrationBuilder.DropColumn(
            name: "ProposedTimeZoneId",
            table: "Schedules");

        migrationBuilder.DropColumn(
            name: "RescheduleRequestCount",
            table: "Schedules");
    }
}
