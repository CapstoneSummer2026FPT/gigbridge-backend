using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminDisputeResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remap old dispute status values to new enum:
            // Old: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed
            // New: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed
            // 0 stays 0 (Open)
            // 1 -> 2 (UnderReview)
            // 2 -> 5 (Resolved)
            // 3 -> 6 (Closed)
            migrationBuilder.Sql("""
                UPDATE "Disputes"
                SET "Status" = CASE "Status"
                    WHEN 1 THEN 2
                    WHEN 2 THEN 5
                    WHEN 3 THEN 6
                    ELSE "Status"
                END
                WHERE "Status" IN (1, 2, 3);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Disputes",
                type: "integer",
                nullable: false,
                comment: "Enum DisputeStatus: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum DisputeStatus: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAdminId",
                table: "Disputes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_AssignedAdminId",
                table: "Disputes",
                column: "AssignedAdminId");

            migrationBuilder.AddForeignKey(
                name: "Disputes_AssignedAdminId_fkey",
                table: "Disputes",
                column: "AssignedAdminId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Disputes_AssignedAdminId_fkey",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_AssignedAdminId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Disputes");

            // Revert status values back to old mapping
            // 2 -> 1 (UnderReview)
            // 5 -> 2 (Resolved)
            // 6 -> 3 (Closed)
            migrationBuilder.Sql("""
                UPDATE "Disputes"
                SET "Status" = CASE "Status"
                    WHEN 2 THEN 1
                    WHEN 5 THEN 2
                    WHEN 6 THEN 3
                    ELSE "Status"
                END
                WHERE "Status" IN (2, 5, 6);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Disputes",
                type: "integer",
                nullable: false,
                comment: "Enum DisputeStatus: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum DisputeStatus: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed");
        }
    }
}
