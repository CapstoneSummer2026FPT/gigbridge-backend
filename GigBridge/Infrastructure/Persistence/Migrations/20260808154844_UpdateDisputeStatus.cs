using Domain.Enums.Disputes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDisputeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remap dispute status values from the 7-value intermediate scheme
            // back to the 4-value enum:
            // Old: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence,
            //      4=DecisionPending, 5=Resolved, 6=Closed
            // New: 0=WaitingAdmin, 1=InProgress, 2=Resolved, 3=Closed
            // 0 stays 0 (Open -> WaitingAdmin)
            // 1 -> 0 (WaitingAdmin)
            // 2 -> 1 (UnderReview -> InProgress)
            // 3 -> 1 (WaitingEvidence -> InProgress)
            // 4 -> 1 (DecisionPending -> InProgress)
            // 5 -> 2 (Resolved)
            // 6 -> 3 (Closed)
            migrationBuilder.Sql("""
                UPDATE "Disputes"
                SET "Status" = CASE "Status"
                    WHEN 1 THEN 0
                    WHEN 2 THEN 1
                    WHEN 3 THEN 1
                    WHEN 4 THEN 1
                    WHEN 5 THEN 2
                    WHEN 6 THEN 3
                    ELSE "Status"
                END
                WHERE "Status" IN (1, 2, 3, 4, 5, 6);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Disputes",
                type: "integer",
                nullable: false,
                comment: "Enum DisputeStatus: 0=WaitingAdmin, 1=InProgress, 2=Resolved, 3=Closed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum DisputeStatus: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the remap back to the 7-value intermediate scheme.
            // 0 -> 1 (WaitingAdmin)
            // 1 -> 2 (UnderReview)
            // 2 -> 5 (Resolved)
            // 3 -> 6 (Closed)
            migrationBuilder.Sql("""
                UPDATE "Disputes"
                SET "Status" = CASE "Status"
                    WHEN 0 THEN 1
                    WHEN 1 THEN 2
                    WHEN 2 THEN 5
                    WHEN 3 THEN 6
                    ELSE "Status"
                END
                WHERE "Status" IN (0, 1, 2, 3);
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Disputes",
                type: "integer",
                nullable: false,
                comment: "Enum DisputeStatus: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum DisputeStatus: 0=WaitingAdmin, 1=InProgress, 2=Resolved, 3=Closed");
        }
    }
}
