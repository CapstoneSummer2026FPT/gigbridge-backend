using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoreContractDraftEscrow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Contracts",
                type: "integer",
                nullable: false,
                comment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=PendingEscrow, 3=PendingSignature, 4=Active, 5=Completed, 6=Cancelled, 7=Disputed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ContractStatus: 0=Active, 1=Completed, 2=Cancelled, 3=Disputed");

            migrationBuilder.AlterColumn<Guid>(
                name: "FreelancerProfilesId",
                table: "Contracts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.Sql(
                """
                UPDATE "Contracts"
                SET "Status" = CASE "Status"
                    WHEN 0 THEN 4
                    WHEN 1 THEN 5
                    WHEN 2 THEN 6
                    WHEN 3 THEN 7
                    ELSE "Status"
                END;
                """);

            migrationBuilder.CreateTable(
                name: "ContractEscrows",
                columns: table => new
                {
                    ContractEscrowId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FundedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiredPercentage = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false, defaultValue: 0.8m),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValueSql: "'VND'::character varying"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum ContractEscrowStatus: 0=PendingFunding, 1=PartiallyFunded, 2=Funded, 3=PartiallyReleased, 4=Released, 5=Refunded, 6=Cancelled, 7=Disputed"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ContractEscrows_pkey", x => x.ContractEscrowId);
                    table.ForeignKey(
                        name: "ContractEscrows_cont_ContractsId_fkey",
                        column: x => x.ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                });

            migrationBuilder.CreateTable(
                name: "EscrowTransactions",
                columns: table => new
                {
                    EscrowTransactionId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractEscrowId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestonesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum EscrowTransactionType: 0=Deposit, 1=ReleaseToFreelancer, 2=RefundToClient, 3=PlatformFee, 4=Adjustment"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum EscrowTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled"),
                    PaymentGateway = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayTransactionCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("EscrowTransactions_pkey", x => x.EscrowTransactionId);
                    table.ForeignKey(
                        name: "EscrowTransactions_cEsc_ContractEscrowId_fkey",
                        column: x => x.ContractEscrowId,
                        principalTable: "ContractEscrows",
                        principalColumn: "ContractEscrowId");
                    table.ForeignKey(
                        name: "EscrowTransactions_mStone_MilestonesId_fkey",
                        column: x => x.MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractEscrows_ContractsId",
                table: "ContractEscrows",
                column: "ContractsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractEscrows_Status",
                table: "ContractEscrows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_ContractEscrowId",
                table: "EscrowTransactions",
                column: "ContractEscrowId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_GatewayTransactionCode",
                table: "EscrowTransactions",
                column: "GatewayTransactionCode");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_MilestonesId",
                table: "EscrowTransactions",
                column: "MilestonesId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_Status",
                table: "EscrowTransactions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscrowTransactions");

            migrationBuilder.DropTable(
                name: "ContractEscrows");

            migrationBuilder.Sql(
                """
                UPDATE "Contracts"
                SET "Status" = CASE "Status"
                    WHEN 4 THEN 0
                    WHEN 5 THEN 1
                    WHEN 6 THEN 2
                    WHEN 7 THEN 3
                    ELSE "Status"
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Contracts",
                type: "integer",
                nullable: false,
                comment: "Enum ContractStatus: 0=Active, 1=Completed, 2=Cancelled, 3=Disputed",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=PendingEscrow, 3=PendingSignature, 4=Active, 5=Completed, 6=Cancelled, 7=Disputed");

            migrationBuilder.AlterColumn<Guid>(
                name: "FreelancerProfilesId",
                table: "Contracts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
