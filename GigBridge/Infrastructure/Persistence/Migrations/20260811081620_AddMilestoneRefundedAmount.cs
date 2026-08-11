using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneRefundedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Milestones",
                type: "integer",
                nullable: false,
                comment: "Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed, 7=Cancelled, 8=Completed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed");

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Milestones",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Milestones");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Milestones",
                type: "integer",
                nullable: false,
                comment: "Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed, 7=Cancelled, 8=Completed");
        }
    }
}
