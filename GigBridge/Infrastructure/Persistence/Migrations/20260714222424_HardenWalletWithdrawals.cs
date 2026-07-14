using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenWalletWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankBin",
                table: "WalletWithdrawals",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankBin",
                table: "BankAccounts",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "BankAccounts"
                SET "BankBin" = "BankCode"
                WHERE "BankCode" ~ '^[0-9]{6}$';

                UPDATE "WalletWithdrawals"
                SET "BankBin" = "BankCode"
                WHERE "BankCode" ~ '^[0-9]{6}$';

                UPDATE "BankAccounts"
                SET "Status" = 1, "IsDefault" = FALSE
                WHERE "BankBin" IS NULL;

                UPDATE "WalletWithdrawals" SET "Metadata" = NULL;
                UPDATE "PayoutWebhookLogs" SET "RawPayload" = '{}'::jsonb;

                DO $$
                DECLARE
                    wallet_record RECORD;
                    transaction_record RECORD;
                    non_withdrawable NUMERIC(18,4);
                    withdrawable NUMERIC(18,4);
                    consumed NUMERIC(18,4);
                BEGIN
                    FOR wallet_record IN
                        SELECT wallet."UserWalletsId", wallet."AvailableTokens"
                        FROM "UserWallets" wallet
                        JOIN "Users" usr ON usr."UserId" = wallet."UserId"
                        WHERE usr."Role" = 1
                    LOOP
                        non_withdrawable := 0;
                        withdrawable := 0;

                        FOR transaction_record IN
                            SELECT "Type", "TokenAmount"
                            FROM "WalletTransactions"
                            WHERE "UserWalletsId" = wallet_record."UserWalletsId"
                              AND "Status" = 1
                            ORDER BY "CreatedAt", "WalletTransactionsId"
                        LOOP
                            CASE
                                WHEN transaction_record."Type" IN (0, 1, 4) THEN
                                    non_withdrawable := non_withdrawable + transaction_record."TokenAmount";
                                WHEN transaction_record."Type" = 3 THEN
                                    withdrawable := withdrawable + transaction_record."TokenAmount";
                                WHEN transaction_record."Type" = 6 THEN
                                    withdrawable := GREATEST(0, withdrawable - transaction_record."TokenAmount");
                                WHEN transaction_record."Type" = 8 THEN
                                    withdrawable := withdrawable + transaction_record."TokenAmount";
                                WHEN transaction_record."Type" IN (2, 5, 10, 11) THEN
                                    consumed := LEAST(non_withdrawable, transaction_record."TokenAmount");
                                    non_withdrawable := non_withdrawable - consumed;
                                    withdrawable := GREATEST(
                                        0,
                                        withdrawable - (transaction_record."TokenAmount" - consumed));
                                ELSE NULL;
                            END CASE;
                        END LOOP;

                        UPDATE "UserWallets"
                        SET "WithdrawableTokens" = LEAST(
                            wallet_record."AvailableTokens",
                            GREATEST(0, withdrawable))
                        WHERE "UserWalletsId" = wallet_record."UserWalletsId";
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropColumn(
                name: "BankBin",
                table: "WalletWithdrawals");

            migrationBuilder.DropColumn(
                name: "BankBin",
                table: "BankAccounts");
        }
    }
}
