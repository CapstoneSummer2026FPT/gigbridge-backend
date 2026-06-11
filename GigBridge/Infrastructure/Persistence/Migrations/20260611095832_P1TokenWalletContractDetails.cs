using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1TokenWalletContractDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "ESignTemplates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "CONTRACT_FIXED_PRICE");

            migrationBuilder.AddColumn<string>(
                name: "CancellationTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfidentialityTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntellectualPropertyTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeOfWork",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserWallets",
                columns: table => new
                {
                    UserWalletsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableTokens = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    HeldTokens = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserWallets_pkey", x => x.UserWalletsId);
                    table.CheckConstraint("CK_UserWallets_AvailableTokens_NonNegative", "\"AvailableTokens\" >= 0");
                    table.CheckConstraint("CK_UserWallets_HeldTokens_NonNegative", "\"HeldTokens\" >= 0");
                    table.ForeignKey(
                        name: "UserWallets_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    WalletTransactionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserWalletsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractEscrowId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    VndAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum WalletTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled"),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GatewayProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayOrderCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayTransactionCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("WalletTransactions_pkey", x => x.WalletTransactionsId);
                    table.ForeignKey(
                        name: "WalletTransactions_cEsc_ContractEscrowId_fkey",
                        column: x => x.ContractEscrowId,
                        principalTable: "ContractEscrows",
                        principalColumn: "ContractEscrowId");
                    table.ForeignKey(
                        name: "WalletTransactions_cont_ContractsId_fkey",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                    table.ForeignKey(
                        name: "WalletTransactions_uWal_UserWalletsId_fkey",
                        column: x => x.UserWalletsId,
                        principalTable: "UserWallets",
                        principalColumn: "UserWalletsId");
                    table.ForeignKey(
                        name: "WalletTransactions_usr_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ESignTemplates_TemplateCode_IsActive",
                table: "ESignTemplates",
                columns: new[] { "TemplateCode", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWallets_UserId",
                table: "UserWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ContractEscrowId",
                table: "WalletTransactions",
                column: "ContractEscrowId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_ContractsId",
                table: "WalletTransactions",
                column: "ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_GatewayOrderCode",
                table: "WalletTransactions",
                column: "GatewayOrderCode");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_GatewayTransactionCode",
                table: "WalletTransactions",
                column: "GatewayTransactionCode");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_Status",
                table: "WalletTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_Type",
                table: "WalletTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserId_IdempotencyKey",
                table: "WalletTransactions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_UserWalletsId",
                table: "WalletTransactions",
                column: "UserWalletsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "UserWallets");

            migrationBuilder.DropIndex(
                name: "IX_ESignTemplates_TemplateCode_IsActive",
                table: "ESignTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "ESignTemplates");

            migrationBuilder.DropColumn(
                name: "CancellationTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ConfidentialityTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DisputeTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IntellectualPropertyTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ScopeOfWork",
                table: "Contracts");

        }
    }
}
