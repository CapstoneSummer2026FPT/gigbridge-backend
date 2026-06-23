using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleMeetIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeetingAttempt",
                table: "Schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingFailureCode",
                table: "Schedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingJoinUri",
                table: "Schedules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MeetingLastAttemptAt",
                table: "Schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingProvider",
                table: "Schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingSpaceName",
                table: "Schedules",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeetingStatus",
                table: "Schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GoogleMeetConnections",
                columns: table => new
                {
                    GoogleMeetConnectionId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    GoogleEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    GrantedScopes = table.Column<string>(type: "text", nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleMeetConnections", x => x.GoogleMeetConnectionId);
                    table.ForeignKey(
                        name: "FK_GoogleMeetConnections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleMeetOAuthStates",
                columns: table => new
                {
                    GoogleMeetOAuthStateId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NonceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedCodeVerifier = table.Column<string>(type: "text", nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrontendReturnPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleMeetOAuthStates", x => x.GoogleMeetOAuthStateId);
                    table.ForeignKey(
                        name: "FK_GoogleMeetOAuthStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoogleMeetProvisioningJobs",
                columns: table => new
                {
                    GoogleMeetProvisioningJobId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnedSpaceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReturnedJoinUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleMeetProvisioningJobs", x => x.GoogleMeetProvisioningJobId);
                    table.ForeignKey(
                        name: "FK_GoogleMeetProvisioningJobs_Schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "Schedules",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoogleMeetProvisioningJobs_Users_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetConnections_UserId_ConnectedAt",
                table: "GoogleMeetConnections",
                columns: new[] { "UserId", "ConnectedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetConnections_UserId_DisconnectedAt",
                table: "GoogleMeetConnections",
                columns: new[] { "UserId", "DisconnectedAt" },
                unique: true,
                filter: "\"DisconnectedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetOAuthStates_FlowId",
                table: "GoogleMeetOAuthStates",
                column: "FlowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetOAuthStates_StateHash",
                table: "GoogleMeetOAuthStates",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetOAuthStates_UserId",
                table: "GoogleMeetOAuthStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetProvisioningJobs_OrganizerUserId",
                table: "GoogleMeetProvisioningJobs",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetProvisioningJobs_ScheduleId_Attempt",
                table: "GoogleMeetProvisioningJobs",
                columns: new[] { "ScheduleId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetProvisioningJobs_ScheduleId_Status",
                table: "GoogleMeetProvisioningJobs",
                columns: new[] { "ScheduleId", "Status" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleMeetProvisioningJobs_Status_CreatedAt",
                table: "GoogleMeetProvisioningJobs",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleMeetConnections");

            migrationBuilder.DropTable(
                name: "GoogleMeetOAuthStates");

            migrationBuilder.DropTable(
                name: "GoogleMeetProvisioningJobs");

            migrationBuilder.DropColumn(
                name: "MeetingAttempt",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingFailureCode",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingJoinUri",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingLastAttemptAt",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingProvider",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingSpaceName",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "MeetingStatus",
                table: "Schedules");
        }
    }
}
