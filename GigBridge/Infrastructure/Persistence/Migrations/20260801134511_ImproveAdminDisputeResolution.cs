using Domain.Enums.Accounts;
using Domain.Enums.Contracts.Escrow;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

public partial class ImproveAdminDisputeResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>("ViolationCount", "Users", "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>("IsFlagged", "Users", "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<int>("AccountStatus", "Users", "integer", nullable: false, defaultValue: 0,
            comment: "Enum AccountStatus: 0=Active, 1=Suspended, 2=Banned");
        migrationBuilder.AddColumn<DateTime>("BannedAt", "Users", "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>("BanReason", "Users", "character varying(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<decimal>("PenaltyAmount", "DisputeMilestoneDecisions", "numeric(18,2)",
            precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("Reason", "DisputeMilestoneDecisions", "character varying(2000)",
            maxLength: 2000, nullable: true);

        migrationBuilder.CreateTable(
            name: "UserViolations",
            columns: table => new
            {
                UserViolationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                ViolationNumber = table.Column<int>(type: "integer", nullable: false),
                ViolationType = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ActionTaken = table.Column<int>(type: "integer", nullable: false),
                SuspendedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserViolations", x => x.UserViolationId);
                table.CheckConstraint("CK_UserViolations_ViolationNumber_Positive", "\"ViolationNumber\" > 0");
                table.ForeignKey("FK_UserViolations_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Users_CreatedByAdminId", x => x.CreatedByAdminId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Disputes_DisputeId", x => x.DisputeId, "Disputes", "DisputesId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Contracts_ContractId", x => x.ContractId, "Contracts", "ContractsId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Milestones_MilestoneId", x => x.MilestoneId, "Milestones", "MilestonesId", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "DisputePenalties",
            columns: table => new
            {
                DisputePenaltyId = table.Column<Guid>(type: "uuid", nullable: false),
                DisputeId = table.Column<Guid>(type: "uuid", nullable: false),
                ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                ViolatingUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientDebitWalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true,
                    comment: "References the client-side wallet transaction that debits held escrow tokens; no destination wallet transaction exists."),
                EscrowTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false,
                    comment: "Enum EscrowTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DisputePenalties", x => x.DisputePenaltyId);
                table.CheckConstraint("CK_DisputePenalties_Amount_Positive", "\"Amount\" > 0");
                table.ForeignKey("FK_DisputePenalties_Disputes_DisputeId", x => x.DisputeId, "Disputes", "DisputesId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_Contracts_ContractId", x => x.ContractId, "Contracts", "ContractsId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_Milestones_MilestoneId", x => x.MilestoneId, "Milestones", "MilestonesId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_Users_ViolatingUserId", x => x.ViolatingUserId, "Users", "UserId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_DisputePenalties_Users_CreatedByAdminId", x => x.CreatedByAdminId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_WalletTransactions_ClientDebitWalletTransactionId", x => x.ClientDebitWalletTransactionId, "WalletTransactions", "WalletTransactionsId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_EscrowTransactions_EscrowTransactionId", x => x.EscrowTransactionId, "EscrowTransactions", "EscrowTransactionId", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_UserViolations_UserId_DisputeId", "UserViolations", new[] { "UserId", "DisputeId" }, unique: true);
        migrationBuilder.CreateIndex("IX_UserViolations_DisputeId", "UserViolations", "DisputeId");
        migrationBuilder.CreateIndex("IX_UserViolations_ContractId", "UserViolations", "ContractId");
        migrationBuilder.CreateIndex("IX_UserViolations_MilestoneId", "UserViolations", "MilestoneId");
        migrationBuilder.CreateIndex("IX_UserViolations_CreatedByAdminId", "UserViolations", "CreatedByAdminId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_DisputeId_MilestoneId", "DisputePenalties", new[] { "DisputeId", "MilestoneId" }, unique: true);
        migrationBuilder.CreateIndex("IX_DisputePenalties_ContractId", "DisputePenalties", "ContractId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_MilestoneId", "DisputePenalties", "MilestoneId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_ViolatingUserId", "DisputePenalties", "ViolatingUserId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_CreatedByAdminId", "DisputePenalties", "CreatedByAdminId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_ClientDebitWalletTransactionId", "DisputePenalties", "ClientDebitWalletTransactionId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_EscrowTransactionId", "DisputePenalties", "EscrowTransactionId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_DisputeMilestoneDecisions_AllocationAmounts_NonNegative",
            table: "DisputeMilestoneDecisions",
            sql: "\"AdditionalReleaseAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"PenaltyAmount\" >= 0");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("DisputePenalties");
        migrationBuilder.DropTable("UserViolations");
        migrationBuilder.DropCheckConstraint(
            name: "CK_DisputeMilestoneDecisions_AllocationAmounts_NonNegative",
            table: "DisputeMilestoneDecisions");
        migrationBuilder.DropColumn("PenaltyAmount", "DisputeMilestoneDecisions");
        migrationBuilder.DropColumn("Reason", "DisputeMilestoneDecisions");
        migrationBuilder.DropColumn("ViolationCount", "Users");
        migrationBuilder.DropColumn("IsFlagged", "Users");
        migrationBuilder.DropColumn("AccountStatus", "Users");
        migrationBuilder.DropColumn("BannedAt", "Users");
        migrationBuilder.DropColumn("BanReason", "Users");
    }
}
