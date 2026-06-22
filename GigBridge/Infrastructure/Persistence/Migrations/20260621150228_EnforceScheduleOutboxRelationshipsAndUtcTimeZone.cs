using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceScheduleOutboxRelationshipsAndUtcTimeZone : Migration
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
                defaultValue: "UTC",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "Asia/Bangkok");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOutboxes_RecipientUserId",
                table: "DeliveryOutboxes",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOutboxes_ScheduleId",
                table: "DeliveryOutboxes",
                column: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOutboxes_Schedules_ScheduleId",
                table: "DeliveryOutboxes",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "ScheduleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOutboxes_Users_RecipientUserId",
                table: "DeliveryOutboxes",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOutboxes_Schedules_ScheduleId",
                table: "DeliveryOutboxes");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOutboxes_Users_RecipientUserId",
                table: "DeliveryOutboxes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOutboxes_RecipientUserId",
                table: "DeliveryOutboxes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOutboxes_ScheduleId",
                table: "DeliveryOutboxes");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZoneId",
                table: "Schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Bangkok",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "UTC");
        }
    }
}
