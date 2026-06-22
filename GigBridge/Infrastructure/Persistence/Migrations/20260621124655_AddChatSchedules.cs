using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleEventSequence",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleEventType",
                table: "Messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryOutboxes",
                columns: table => new
                {
                    DeliveryOutboxId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    DeliveryKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventSequence = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOutboxes", x => x.DeliveryOutboxId);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Asia/Bangkok"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EditCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_Schedules_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationsId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Schedules_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schedules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_UnreadSchedule_User_Reference",
                table: "Notifications",
                columns: new[] { "UserId", "ReferenceId" },
                unique: true,
                filter: "\"Type\" = 13 AND \"ReferenceId\" IS NOT NULL AND \"IsRead\" IS NOT TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ScheduleId_EventSequence",
                table: "Messages",
                columns: new[] { "ScheduleId", "ScheduleEventSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOutboxes_DeliveryKey",
                table: "DeliveryOutboxes",
                column: "DeliveryKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOutboxes_Status_NextAttemptAt",
                table: "DeliveryOutboxes",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_CancelledByUserId",
                table: "Schedules",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_ConversationId_ScheduledAtUtc",
                table: "Schedules",
                columns: new[] { "ConversationId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_CreatedByUserId",
                table: "Schedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_Status_ScheduledAtUtc",
                table: "Schedules",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "Messages_sch_ScheduleId_fkey",
                table: "Messages",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "ScheduleId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Messages_sch_ScheduleId_fkey",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "DeliveryOutboxes");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropIndex(
                name: "UX_Notifications_UnreadSchedule_User_Reference",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ScheduleId_EventSequence",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ScheduleEventSequence",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ScheduleEventType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Messages");
        }
    }
}
