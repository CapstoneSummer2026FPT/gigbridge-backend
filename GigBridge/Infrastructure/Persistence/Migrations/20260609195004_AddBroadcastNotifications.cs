using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBroadcastNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BroadcastNotifications",
                columns: table => new
                {
                    BroadcastNotificationId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum NotificationType"),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TargetScope = table.Column<int>(type: "integer", nullable: false, comment: "Enum NotificationTarget"),
                    TargetRole = table.Column<int>(type: "integer", nullable: true, comment: "Enum UserRole"),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("BroadcastNotifications_pkey", x => x.BroadcastNotificationId);
                    table.ForeignKey(
                        name: "BroadcastNotifications_CreatedByAdminId_fkey",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "BroadcastNotificationRecipients",
                columns: table => new
                {
                    BroadcastNotificationRecipientId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BroadcastNotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BroadcastNotificationRecipients_pkey", x => x.BroadcastNotificationRecipientId);
                    table.ForeignKey(
                        name: "BroadcastRecipients_BroadcastNotificationId_fkey",
                        column: x => x.BroadcastNotificationId,
                        principalTable: "BroadcastNotifications",
                        principalColumn: "BroadcastNotificationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "BroadcastRecipients_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Unread_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"IsRead\" IS NOT TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_BroadcastNotificationId_UserId",
                table: "BroadcastNotificationRecipients",
                columns: new[] { "BroadcastNotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_UserId_CreatedAt",
                table: "BroadcastNotificationRecipients",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastRecipients_UserId_IsRead",
                table: "BroadcastNotificationRecipients",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastNotifications_CreatedAt",
                table: "BroadcastNotifications",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastNotifications_CreatedByAdminId",
                table: "BroadcastNotifications",
                column: "CreatedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastNotificationRecipients");

            migrationBuilder.DropTable(
                name: "BroadcastNotifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_Unread_UserId_CreatedAt",
                table: "Notifications");
        }
    }
}
