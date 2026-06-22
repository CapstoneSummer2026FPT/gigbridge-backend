using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseVietnamScheduleTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "Schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "UTC");

            migrationBuilder.Sql("""
                UPDATE "Schedules"
                SET "TimeZoneId" = 'Asia/Ho_Chi_Minh'
                WHERE "TimeZoneId" IN ('UTC', 'Asia/Bangkok');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Schedules"
                SET "TimeZoneId" = 'UTC'
                WHERE "TimeZoneId" = 'Asia/Ho_Chi_Minh';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "Schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "UTC",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "Asia/Ho_Chi_Minh");
        }
    }
}
