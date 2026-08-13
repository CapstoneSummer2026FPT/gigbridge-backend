using Domain.Enums.Reports;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAdminUserAndAccountReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AdminAuditLogs");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AdminAuditLogs",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CorrelationId",
                table: "AdminAuditLogs",
                column: "CorrelationId");

            migrationBuilder.DropIndex(
                name: "IX_UserViolations_UserId_DisputeId",
                table: "UserViolations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DisputeId",
                table: "UserViolations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                table: "UserViolations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ManualActionId",
                table: "UserViolations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportId",
                table: "UserViolations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "UserViolations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing rows were created exclusively by the dispute workflow.
            migrationBuilder.Sql("UPDATE \"UserViolations\" SET \"SourceType\" = 0;");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAdminId",
                table: "Reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Reports",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionAction",
                table: "Reports",
                type: "integer",
                nullable: true,
                comment: "Enum AccountReportResolutionAction: 0=None, 1=Warning, 2=Suspension, 3=PermanentBan");

            migrationBuilder.CreateTable(
                name: "ReportEvidences",
                columns: table => new
                {
                    ReportEvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportEvidences", x => x.ReportEvidenceId);
                    table.CheckConstraint("CK_ReportEvidences_FileSize_Positive", "\"FileSize\" > 0");
                    table.ForeignKey(
                        name: "FK_ReportEvidences_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "ReportsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportEvidences_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserViolations_ReportId",
                table: "UserViolations",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_UserViolations_UserId_DisputeId",
                table: "UserViolations",
                columns: new[] { "UserId", "DisputeId" },
                unique: true,
                filter: "\"DisputeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserViolations_UserId_ManualActionId",
                table: "UserViolations",
                columns: new[] { "UserId", "ManualActionId" },
                unique: true,
                filter: "\"ManualActionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserViolations_UserId_ReportId",
                table: "UserViolations",
                columns: new[] { "UserId", "ReportId" },
                unique: true,
                filter: "\"ReportId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserViolations_ExactlyOneSource",
                table: "UserViolations",
                sql: "(\"SourceType\" = 0 AND \"DisputeId\" IS NOT NULL AND \"ContractId\" IS NOT NULL AND \"ReportId\" IS NULL AND \"ManualActionId\" IS NULL) OR (\"SourceType\" = 1 AND \"DisputeId\" IS NULL AND \"ReportId\" IS NOT NULL AND \"ManualActionId\" IS NULL AND \"ContractId\" IS NULL AND \"MilestoneId\" IS NULL) OR (\"SourceType\" = 2 AND \"DisputeId\" IS NULL AND \"ReportId\" IS NULL AND \"ManualActionId\" IS NOT NULL AND \"ContractId\" IS NULL AND \"MilestoneId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AssignedAdminId",
                table: "Reports",
                column: "AssignedAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEvidences_ReportId",
                table: "ReportEvidences",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportEvidences_UploadedByUserId",
                table: "ReportEvidences",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "Reports_AssignedAdminId_fkey",
                table: "Reports",
                column: "AssignedAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserViolations_Reports_ReportId",
                table: "UserViolations",
                column: "ReportId",
                principalTable: "Reports",
                principalColumn: "ReportsId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "UserViolations"
                        WHERE "SourceType" IN (1, 2)
                           OR "ReportId" IS NOT NULL
                           OR "ManualActionId" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back CompleteAdminUserAndAccountReports while report or manual violation history exists.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AdminAuditLogs_CorrelationId",
                table: "AdminAuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AdminAuditLogs");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AdminAuditLogs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.DropForeignKey(
                name: "Reports_AssignedAdminId_fkey",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_UserViolations_Reports_ReportId",
                table: "UserViolations");

            migrationBuilder.DropTable(
                name: "ReportEvidences");

            migrationBuilder.DropIndex(
                name: "IX_UserViolations_ReportId",
                table: "UserViolations");

            migrationBuilder.DropIndex(
                name: "IX_UserViolations_UserId_DisputeId",
                table: "UserViolations");

            migrationBuilder.DropIndex(
                name: "IX_UserViolations_UserId_ManualActionId",
                table: "UserViolations");

            migrationBuilder.DropIndex(
                name: "IX_UserViolations_UserId_ReportId",
                table: "UserViolations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserViolations_ExactlyOneSource",
                table: "UserViolations");

            migrationBuilder.DropIndex(
                name: "IX_Reports_AssignedAdminId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ManualActionId",
                table: "UserViolations");

            migrationBuilder.DropColumn(
                name: "ReportId",
                table: "UserViolations");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "UserViolations");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ResolutionAction",
                table: "Reports");

            migrationBuilder.AlterColumn<Guid>(
                name: "DisputeId",
                table: "UserViolations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractId",
                table: "UserViolations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserViolations_UserId_DisputeId",
                table: "UserViolations",
                columns: new[] { "UserId", "DisputeId" },
                unique: true);
        }
    }
}
