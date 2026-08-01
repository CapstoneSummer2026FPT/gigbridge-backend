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
        migrationBuilder.Sql("UPDATE \"Users\" SET \"AccountStatus\" = 1 WHERE \"SuspendedUntil\" IS NOT NULL AND \"SuspendedUntil\" > now();");
        migrationBuilder.Sql("UPDATE \"Users\" SET \"AccountStatus\" = 2 WHERE \"IsActive\" = false;");

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
                table.ForeignKey("FK_UserViolations_Users_UserId", x => x.UserId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Users_CreatedByAdminId", x => x.CreatedByAdminId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_UserViolations_Disputes_DisputeId", x => x.DisputeId, "Disputes", "DisputesId", onDelete: ReferentialAction.Cascade);
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
                WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                EscrowTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false,
                    comment: "Enum EscrowTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DisputePenalties", x => x.DisputePenaltyId);
                table.ForeignKey("FK_DisputePenalties_Disputes_DisputeId", x => x.DisputeId, "Disputes", "DisputesId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_DisputePenalties_Contracts_ContractId", x => x.ContractId, "Contracts", "ContractsId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_Milestones_MilestoneId", x => x.MilestoneId, "Milestones", "MilestonesId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_Users_ViolatingUserId", x => x.ViolatingUserId, "Users", "UserId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_DisputePenalties_Users_CreatedByAdminId", x => x.CreatedByAdminId, "Users", "UserId", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DisputePenalties_WalletTransactions_WalletTransactionId", x => x.WalletTransactionId, "WalletTransactions", "WalletTransactionsId", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_DisputePenalties_EscrowTransactions_EscrowTransactionId", x => x.EscrowTransactionId, "EscrowTransactions", "EscrowTransactionId", onDelete: ReferentialAction.SetNull);
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
        migrationBuilder.CreateIndex("IX_DisputePenalties_WalletTransactionId", "DisputePenalties", "WalletTransactionId");
        migrationBuilder.CreateIndex("IX_DisputePenalties_EscrowTransactionId", "DisputePenalties", "EscrowTransactionId");

        migrationBuilder.InsertData(
            table: "Users",
            columns: new[] { "UserId", "FullName", "Email", "Role", "IsEmailVerified", "IsActive", "IsSetup", "AccountStatus", "ViolationCount", "IsFlagged", "Provider", "ProviderId", "CreatedAt" },
            values: new object[] { Guid.Parse("00000000-0000-0000-0000-00000000d15c"), "GigBridge Dispute Penalty Account", "system.dispute.penalties@gigbridge.local", 2, true, false, true, 0, 0, false, "System", "dispute-penalty-wallet", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) });
        migrationBuilder.InsertData(
            table: "UserWallets",
            columns: new[] { "UserWalletsId", "UserId", "AvailableTokens", "WithdrawableTokens", "HeldTokens", "PendingWithdrawalTokens", "Version", "CreatedAt" },
            values: new object[] { Guid.Parse("00000000-0000-0000-0000-00000000d15d"), Guid.Parse("00000000-0000-0000-0000-00000000d15c"), 0m, 0m, 0m, 0m, 1, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData("UserWallets", "UserWalletsId", Guid.Parse("00000000-0000-0000-0000-00000000d15d"));
        migrationBuilder.DeleteData("Users", "UserId", Guid.Parse("00000000-0000-0000-0000-00000000d15c"));
        migrationBuilder.DropTable("DisputePenalties");
        migrationBuilder.DropTable("UserViolations");
        migrationBuilder.DropColumn("PenaltyAmount", "DisputeMilestoneDecisions");
        migrationBuilder.DropColumn("Reason", "DisputeMilestoneDecisions");
        migrationBuilder.DropColumn("ViolationCount", "Users");
        migrationBuilder.DropColumn("IsFlagged", "Users");
        migrationBuilder.DropColumn("AccountStatus", "Users");
        migrationBuilder.DropColumn("BannedAt", "Users");
        migrationBuilder.DropColumn("BanReason", "Users");
    }
}
