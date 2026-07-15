using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawableTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WithdrawableTokens",
                table: "UserWallets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_MaxAvailable",
                table: "UserWallets",
                sql: "\"WithdrawableTokens\" <= \"AvailableTokens\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_NonNegative",
                table: "UserWallets",
                sql: "\"WithdrawableTokens\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_MaxAvailable",
                table: "UserWallets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_NonNegative",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "WithdrawableTokens",
                table: "UserWallets");
        }
    }
}
