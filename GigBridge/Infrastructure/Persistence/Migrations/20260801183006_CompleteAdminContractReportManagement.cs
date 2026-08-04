using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAdminContractReportManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Disputes"
                        WHERE "RelatedReportId" IS NOT NULL
                        GROUP BY "RelatedReportId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce one Dispute per Contract Report because duplicate RelatedReportId values exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes");

            migrationBuilder.DropForeignKey(
                name: "ReportContractAttachments_rc_ReportContractId_fkey",
                table: "ReportContractAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_RelatedReportId",
                table: "Disputes");

            migrationBuilder.AddColumn<int>(
                name: "AdminResolutionAction",
                table: "ReportContracts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminResolutionNote",
                table: "ReportContracts",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdminReviewStatus",
                table: "ReportContracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAdminId",
                table: "ReportContracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "ReportContracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReportContracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"ReportContracts\" SET \"AdminReviewStatus\" = 3 WHERE \"Status\" = 2; " +
                "UPDATE \"ReportContracts\" SET \"AdminReviewStatus\" = 5 WHERE \"Status\" = 3;");

            migrationBuilder.CreateTable(
                name: "ReportContractAdminNotes",
                columns: table => new
                {
                    ReportContractAdminNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportContractAdminNotes", x => x.ReportContractAdminNoteId);
                    table.ForeignKey(
                        name: "FK_ReportContractAdminNotes_ReportContracts_ReportContractId",
                        column: x => x.ReportContractId,
                        principalTable: "ReportContracts",
                        principalColumn: "ReportContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportContractAdminNotes_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportContractInformationRequests",
                columns: table => new
                {
                    InformationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequestedEvidenceOrClarification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportContractInformationRequests", x => x.InformationRequestId);
                    table.CheckConstraint("CK_ReportContractInformationRequests_Status_Valid", "\"Status\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ReportContractInformationRequests_ReportContracts_ReportCon~",
                        column: x => x.ReportContractId,
                        principalTable: "ReportContracts",
                        principalColumn: "ReportContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportContractInformationRequests_Users_RequestedByAdminId",
                        column: x => x.RequestedByAdminId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportContractInformationRequests_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_AdminReviewStatus",
                table: "ReportContracts",
                column: "AdminReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_AssignedAdminId",
                table: "ReportContracts",
                column: "AssignedAdminId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportContracts_AdminReviewStatus_Valid",
                table: "ReportContracts",
                sql: "\"AdminReviewStatus\" BETWEEN 0 AND 6");

            migrationBuilder.CreateIndex(
                name: "UX_Disputes_RelatedReportId",
                table: "Disputes",
                column: "RelatedReportId",
                unique: true,
                filter: "\"RelatedReportId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractAdminNotes_AdminUserId",
                table: "ReportContractAdminNotes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractAdminNotes_ReportContractId_CreatedAt",
                table: "ReportContractAdminNotes",
                columns: new[] { "ReportContractId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractInformationRequests_ReportContractId_RequestI~",
                table: "ReportContractInformationRequests",
                columns: new[] { "ReportContractId", "RequestId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractInformationRequests_RequestedByAdminId",
                table: "ReportContractInformationRequests",
                column: "RequestedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractInformationRequests_TargetUserId",
                table: "ReportContractInformationRequests",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes",
                column: "RelatedReportId",
                principalTable: "ReportContracts",
                principalColumn: "ReportContractId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "ReportContractAttachments_rc_ReportContractId_fkey",
                table: "ReportContractAttachments",
                column: "ReportContractId",
                principalTable: "ReportContracts",
                principalColumn: "ReportContractId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "ReportContracts_usr_AssignedAdminId_fkey",
                table: "ReportContracts",
                column: "AssignedAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes");

            migrationBuilder.DropForeignKey(
                name: "ReportContractAttachments_rc_ReportContractId_fkey",
                table: "ReportContractAttachments");

            migrationBuilder.DropForeignKey(
                name: "ReportContracts_usr_AssignedAdminId_fkey",
                table: "ReportContracts");

            migrationBuilder.DropTable(
                name: "ReportContractAdminNotes");

            migrationBuilder.DropTable(
                name: "ReportContractInformationRequests");

            migrationBuilder.DropIndex(
                name: "IX_ReportContracts_AdminReviewStatus",
                table: "ReportContracts");

            migrationBuilder.DropIndex(
                name: "IX_ReportContracts_AssignedAdminId",
                table: "ReportContracts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportContracts_AdminReviewStatus_Valid",
                table: "ReportContracts");

            migrationBuilder.DropIndex(
                name: "UX_Disputes_RelatedReportId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AdminResolutionAction",
                table: "ReportContracts");

            migrationBuilder.DropColumn(
                name: "AdminResolutionNote",
                table: "ReportContracts");

            migrationBuilder.DropColumn(
                name: "AdminReviewStatus",
                table: "ReportContracts");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "ReportContracts");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "ReportContracts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReportContracts");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_RelatedReportId",
                table: "Disputes",
                column: "RelatedReportId");

            migrationBuilder.AddForeignKey(
                name: "Disputes_rc_RelatedReportId_fkey",
                table: "Disputes",
                column: "RelatedReportId",
                principalTable: "ReportContracts",
                principalColumn: "ReportContractId");

            migrationBuilder.AddForeignKey(
                name: "ReportContractAttachments_rc_ReportContractId_fkey",
                table: "ReportContractAttachments",
                column: "ReportContractId",
                principalTable: "ReportContracts",
                principalColumn: "ReportContractId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
