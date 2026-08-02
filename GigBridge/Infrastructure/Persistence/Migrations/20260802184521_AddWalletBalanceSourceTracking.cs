using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletBalanceSourceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_MaxAvailable",
                table: "UserWallets");

            // Legacy wallets stored AvailableTokens as the total spendable balance,
            // with WithdrawableTokens as an earned subset of that total. The new model
            // stores deposited and earned tokens as independent pools, so remove the
            // earned subset from AvailableTokens while preserving total value:
            //   old available = new deposited + new earned.
            migrationBuilder.Sql(
                """
                UPDATE "UserWallets"
                SET "AvailableTokens" = GREATEST(0, "AvailableTokens" - "WithdrawableTokens");
                """);

            migrationBuilder.AddColumn<int>(
                name: "BalanceSource",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum WalletBalanceSource: 0=Deposited, 1=Earned, 2=HeldDeposited, 3=HeldEarned, 4=PendingWithdrawal, 5=Combined");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositedAmount",
                table: "WalletTransactions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                comment: "Token amount sourced from the deposited pool (AvailableTokens) or held-deposited escrow; null when single-source Earned.");

            migrationBuilder.AddColumn<decimal>(
                name: "EarnedAmount",
                table: "WalletTransactions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                comment: "Token amount sourced from the earned pool (WithdrawableTokens) or held-earned escrow; null when single-source Deposited.");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositedTokens",
                table: "ContractEscrows",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                comment: "Portion of held escrow funded from the deposited (non-withdrawable) pool; restored to AvailableTokens on refund.");

            migrationBuilder.AddColumn<decimal>(
                name: "EarnedTokens",
                table: "ContractEscrows",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                comment: "Portion of held escrow funded from the earned (withdrawable) pool; restored to WithdrawableTokens on refund.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recompose the independent pools into the legacy representation before
            // restoring its WithdrawableTokens <= AvailableTokens invariant.
            migrationBuilder.Sql(
                """
                UPDATE "UserWallets"
                SET "AvailableTokens" = "AvailableTokens" + "WithdrawableTokens";
                """);

            migrationBuilder.DropColumn(
                name: "BalanceSource",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "DepositedAmount",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "EarnedAmount",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "DepositedTokens",
                table: "ContractEscrows");

            migrationBuilder.DropColumn(
                name: "EarnedTokens",
                table: "ContractEscrows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserWallets_WithdrawableTokens_MaxAvailable",
                table: "UserWallets",
                sql: "\"WithdrawableTokens\" <= \"AvailableTokens\"");
        }
    }
}
