using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedPriceCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetType",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "FreelancerProfiles");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "ProposedRate",
                table: "Proposals",
                newName: "ProposedBudget");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProposedBudget",
                table: "Proposals",
                newName: "ProposedRate");

            migrationBuilder.AddColumn<int>(
                name: "BudgetType",
                table: "JobPosts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum BudgetType: 0=Fixed, 1=Hourly");

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "FreelancerProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum PaymentType: 0=Fixed, 1=Hourly");
        }
    }
}
