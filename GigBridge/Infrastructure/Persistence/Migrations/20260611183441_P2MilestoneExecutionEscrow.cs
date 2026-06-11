using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2MilestoneExecutionEscrow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MilestonesId",
                table: "WalletTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReleasedAt",
                table: "Milestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReleasedAmount",
                table: "Milestones",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Milestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "RequiredPercentage",
                table: "ContractEscrows",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 1.0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldDefaultValue: 0.8m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReleasedAmount",
                table: "ContractEscrows",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "ContractEscrows" AS escrow
                SET "RequiredAmount" = contract."TotalBudget",
                    "RequiredPercentage" = 1.0,
                    "Status" = CASE
                        WHEN escrow."FundedAmount" >= contract."TotalBudget" THEN 2
                        WHEN escrow."FundedAmount" > 0 THEN 1
                        ELSE escrow."Status"
                    END
                FROM "Contracts" AS contract
                WHERE escrow."ContractsId" = contract."ContractsId";
                """);

            migrationBuilder.Sql("""
                UPDATE "Contracts" AS contract
                SET "Status" = 5,
                    "UpdatedAt" = now()
                FROM "ContractEscrows" AS escrow
                WHERE escrow."ContractsId" = contract."ContractsId"
                    AND contract."Status" = 7
                    AND escrow."FundedAmount" < contract."TotalBudget";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_MilestonesId",
                table: "WalletTransactions",
                column: "MilestonesId");

            migrationBuilder.AddForeignKey(
                name: "WalletTransactions_mStone_MilestonesId_fkey",
                table: "WalletTransactions",
                column: "MilestonesId",
                principalTable: "Milestones",
                principalColumn: "MilestonesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "WalletTransactions_mStone_MilestonesId_fkey",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_MilestonesId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "MilestonesId",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "LastReleasedAt",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ReleasedAmount",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ReleasedAmount",
                table: "ContractEscrows");

            migrationBuilder.AlterColumn<decimal>(
                name: "RequiredPercentage",
                table: "ContractEscrows",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.8m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldDefaultValue: 1.0m);
        }
    }
}
