using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeEgressRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "ProjectReceipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProjectReceiptArtifacts",
                columns: table => new
                {
                    ProjectReceiptArtifactId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProjectReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactType = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHashSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ArtifactRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReceiptArtifacts", x => x.ProjectReceiptArtifactId);
                    table.CheckConstraint("CK_ProjectReceiptArtifacts_ArtifactType", "\"ArtifactType\" = 1");
                    table.CheckConstraint("CK_ProjectReceiptArtifacts_SizeBytes", "\"SizeBytes\" = octet_length(\"Content\") AND \"SizeBytes\" > 0");
                    table.ForeignKey(
                        name: "FK_ProjectReceiptArtifacts_ProjectReceipts_ProjectReceiptId",
                        column: x => x.ProjectReceiptId,
                        principalTable: "ProjectReceipts",
                        principalColumn: "ProjectReceiptId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRealtimeStates",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationRevision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NotificationUnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConversationRevision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConversationUnreadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRealtimeStates", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserRealtimeStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceiptArtifacts_ProjectReceiptId_ArtifactType",
                table: "ProjectReceiptArtifacts",
                columns: new[] { "ProjectReceiptId", "ArtifactType" },
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "ProjectReceipts" SET "Revision" = 1 WHERE "Revision" = 0;

                INSERT INTO "ProjectReceiptArtifacts"
                    ("ProjectReceiptArtifactId", "ProjectReceiptId", "ArtifactType", "Content",
                     "FileName", "MimeType", "SizeBytes", "ContentHashSha256",
                     "ArtifactRevision", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c."ProjectReceiptId", 1, c."PdfContent",
                       COALESCE(NULLIF(c."PdfFileName", ''), 'GigBridge-receipt.pdf'),
                       COALESCE(NULLIF(c."PdfContentType", ''), 'application/pdf'),
                       octet_length(c."PdfContent"),
                       COALESCE(NULLIF(c."PdfHashSha256", ''), encode(digest(c."PdfContent", 'sha256'), 'hex')),
                       GREATEST(r."ContentRevision", 1), now(), now()
                FROM "ProjectReceiptContents" c
                JOIN "ProjectReceipts" r ON r."ProjectReceiptId" = c."ProjectReceiptId"
                WHERE c."PdfContent" IS NOT NULL AND octet_length(c."PdfContent") > 0
                ON CONFLICT ("ProjectReceiptId", "ArtifactType") DO NOTHING;

                INSERT INTO "UserRealtimeStates"
                    ("UserId", "NotificationRevision", "NotificationUnreadCount",
                     "ConversationRevision", "ConversationUnreadCount", "UpdatedAt")
                SELECT u."UserId",
                       CASE WHEN EXISTS (SELECT 1 FROM "Notifications" n WHERE n."UserId" = u."UserId")
                                  OR EXISTS (SELECT 1 FROM "BroadcastNotificationRecipients" br WHERE br."UserId" = u."UserId")
                            THEN 1 ELSE 0 END,
                       (SELECT count(*)::int FROM "Notifications" n
                         WHERE n."UserId" = u."UserId" AND COALESCE(n."IsRead", false) = false)
                       + (SELECT count(*)::int FROM "BroadcastNotificationRecipients" br
                          JOIN "BroadcastNotifications" b ON b."BroadcastNotificationId" = br."BroadcastNotificationId"
                          WHERE br."UserId" = u."UserId" AND COALESCE(br."IsRead", false) = false
                            AND (b."ExpiresAt" IS NULL OR b."ExpiresAt" > now())),
                       CASE WHEN EXISTS (SELECT 1 FROM "ConversationParticipants" cp WHERE cp."UserId" = u."UserId")
                            THEN 1 ELSE 0 END,
                       COALESCE((SELECT sum(cp."UnreadCount")::int FROM "ConversationParticipants" cp
                                 WHERE cp."UserId" = u."UserId" AND cp."DeletedAt" IS NULL), 0),
                       now()
                FROM "Users" u
                WHERE EXISTS (SELECT 1 FROM "Notifications" n WHERE n."UserId" = u."UserId")
                   OR EXISTS (SELECT 1 FROM "BroadcastNotificationRecipients" br WHERE br."UserId" = u."UserId")
                   OR EXISTS (SELECT 1 FROM "ConversationParticipants" cp WHERE cp."UserId" = u."UserId")
                ON CONFLICT ("UserId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectReceiptArtifacts");

            migrationBuilder.DropTable(
                name: "UserRealtimeStates");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "ProjectReceipts");

        }
    }
}
