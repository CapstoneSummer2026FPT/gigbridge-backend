using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectReceipts",
                columns: table => new
                {
                    ProjectReceiptId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptType = table.Column<int>(type: "integer", nullable: false, comment: "Enum ProjectReceiptType: 0=Client, 1=Freelancer"),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GenerationStatus = table.Column<int>(type: "integer", nullable: false, comment: "Enum ProjectReceiptGenerationStatus: 0=Pending, 1=Processing, 2=Ready, 3=Failed"),
                    GenerationAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextGenerationAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GenerationLeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    GenerationLeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GenerationLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PdfContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PdfFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PdfContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PdfSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    PdfHashSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailStatus = table.Column<int>(type: "integer", nullable: false, comment: "Enum ProjectReceiptEmailStatus: 0=Pending, 1=Delivered, 2=Failed"),
                    EmailAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextEmailAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailLastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReceipts", x => x.ProjectReceiptId);
                    table.ForeignKey(
                        name: "FK_ProjectReceipts_Contracts_ContractsId",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectReceipts_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "NotificationsId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectReceipts_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_ContractsId_ReceiptType",
                table: "ProjectReceipts",
                columns: new[] { "ContractsId", "ReceiptType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_EmailStatus_NextEmailAttemptAt",
                table: "ProjectReceipts",
                columns: new[] { "EmailStatus", "NextEmailAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_GenerationStatus_NextGenerationAttemptAt",
                table: "ProjectReceipts",
                columns: new[] { "GenerationStatus", "NextGenerationAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_NotificationId",
                table: "ProjectReceipts",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_OwnerUserId_IssuedAt",
                table: "ProjectReceipts",
                columns: new[] { "OwnerUserId", "IssuedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReceipts_ReceiptNumber",
                table: "ProjectReceipts",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectReceipts");
        }
    }
}
