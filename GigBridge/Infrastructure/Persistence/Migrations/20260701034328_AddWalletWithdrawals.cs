using Domain.Enums.Wallets;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment");

            migrationBuilder.AddColumn<decimal>(
                name: "PendingWithdrawalTokens",
                table: "UserWallets",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AccountNumberEncrypted = table.Column<string>(type: "text", nullable: false),
                    AccountNumberMasked = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum BankAccountStatus: 0=Active, 1=Disabled"),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("BankAccounts_pkey", x => x.BankAccountId);
                    table.ForeignKey(
                        name: "BankAccounts_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "WalletWithdrawals",
                columns: table => new
                {
                    WalletWithdrawalId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserWalletsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BankAccountNumberEncrypted = table.Column<string>(type: "text", nullable: false),
                    BankAccountNumberMasked = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BankAccountName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TokenAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    VndAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FeeVnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    NetVndAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum WithdrawalStatus: 0=Pending, 1=Processing, 2=SyncRequired, 3=Success, 4=Failed, 5=Cancelled"),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderOrderCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderPayoutId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderTransactionCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderRawStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("WalletWithdrawals_pkey", x => x.WalletWithdrawalId);
                    table.CheckConstraint("CK_WalletWithdrawals_NetVndAmount_Positive", "\"NetVndAmount\" > 0");
                    table.CheckConstraint("CK_WalletWithdrawals_TokenAmount_Positive", "\"TokenAmount\" > 0");
                    table.CheckConstraint("CK_WalletWithdrawals_VndAmount_Positive", "\"VndAmount\" > 0");
                    table.ForeignKey(
                        name: "WalletWithdrawals_bnk_BankAccountId_fkey",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "BankAccountId");
                    table.ForeignKey(
                        name: "WalletWithdrawals_uWal_UserWalletsId_fkey",
                        column: x => x.UserWalletsId,
                        principalTable: "UserWallets",
                        principalColumn: "UserWalletsId");
                    table.ForeignKey(
                        name: "WalletWithdrawals_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PayoutOutboxes",
                columns: table => new
                {
                    PayoutOutboxId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    WalletWithdrawalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum PayoutOutboxStatus: 0=Pending, 1=Processing, 2=Delivered, 3=DeadLettered, 4=Cancelled"),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PayoutOutboxes_pkey", x => x.PayoutOutboxId);
                    table.ForeignKey(
                        name: "PayoutOutboxes_wwd_WalletWithdrawalId_fkey",
                        column: x => x.WalletWithdrawalId,
                        principalTable: "WalletWithdrawals",
                        principalColumn: "WalletWithdrawalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayoutWebhookLogs",
                columns: table => new
                {
                    PayoutWebhookLogId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignatureHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WalletWithdrawalId = table.Column<Guid>(type: "uuid", nullable: true),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false, comment: "Enum PayoutWebhookProcessingStatus: 0=Pending, 1=Processed, 2=Rejected, 3=Failed"),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PayoutWebhookLogs_pkey", x => x.PayoutWebhookLogId);
                    table.ForeignKey(
                        name: "PayoutWebhookLogs_wwd_WalletWithdrawalId_fkey",
                        column: x => x.WalletWithdrawalId,
                        principalTable: "WalletWithdrawals",
                        principalColumn: "WalletWithdrawalId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserWallets_PendingWithdrawalTokens_NonNegative",
                table: "UserWallets",
                sql: "\"PendingWithdrawalTokens\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_UserId",
                table: "BankAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_UserId_IsDefault",
                table: "BankAccounts",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_UserId_Status",
                table: "BankAccounts",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutOutboxes_PayoutKey",
                table: "PayoutOutboxes",
                column: "PayoutKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutOutboxes_Status_NextAttemptAt",
                table: "PayoutOutboxes",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutOutboxes_WalletWithdrawalId",
                table: "PayoutOutboxes",
                column: "WalletWithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_Provider_EventId",
                table: "PayoutWebhookLogs",
                columns: new[] { "Provider", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_Provider_SignatureHash",
                table: "PayoutWebhookLogs",
                columns: new[] { "Provider", "SignatureHash" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutWebhookLogs_WalletWithdrawalId",
                table: "PayoutWebhookLogs",
                column: "WalletWithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_BankAccountId",
                table: "WalletWithdrawals",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_Provider_ProviderPayoutId",
                table: "WalletWithdrawals",
                columns: new[] { "Provider", "ProviderPayoutId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_ProviderOrderCode",
                table: "WalletWithdrawals",
                column: "ProviderOrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_Status",
                table: "WalletWithdrawals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_UserId_CreatedAt",
                table: "WalletWithdrawals",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_UserId_IdempotencyKey",
                table: "WalletWithdrawals",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_UserWalletsId",
                table: "WalletWithdrawals",
                column: "UserWalletsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutOutboxes");

            migrationBuilder.DropTable(
                name: "PayoutWebhookLogs");

            migrationBuilder.DropTable(
                name: "WalletWithdrawals");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserWallets_PendingWithdrawalTokens_NonNegative",
                table: "UserWallets");

            migrationBuilder.DropColumn(
                name: "PendingWithdrawalTokens",
                table: "UserWallets");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "WalletTransactions",
                type: "integer",
                nullable: false,
                comment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee");
        }
    }
}
