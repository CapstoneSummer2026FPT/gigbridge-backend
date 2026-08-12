using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameAuditLogUserToAuditLogWorkSpace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pure rename, not a drop/recreate — preserves any rows already written to
            // AuditLogUsers on environments where the earlier migration was already applied.
            // MigrationBuilder has no fluent helper for renaming a PK/FK constraint, so those
            // use PostgreSQL's ALTER TABLE ... RENAME CONSTRAINT directly.
            migrationBuilder.RenameTable(
                name: "AuditLogUsers",
                newName: "AuditLogWorkSpaces");

            migrationBuilder.RenameColumn(
                name: "AuditLogUsersId",
                table: "AuditLogWorkSpaces",
                newName: "AuditLogWorkSpaceId");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"PK_AuditLogUsers\" TO \"PK_AuditLogWorkSpaces\";");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_ContractId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_ContractId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_ContractId_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_ContractId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_DisputeId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_DisputeId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_DisputeId_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_DisputeId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_MilestoneId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_MilestoneId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_ReportId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_ReportId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogUsers_UserId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogWorkSpaces_UserId");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogUsers_Contracts_ContractId\" TO \"FK_AuditLogWorkSpaces_Contracts_ContractId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogUsers_Disputes_DisputeId\" TO \"FK_AuditLogWorkSpaces_Disputes_DisputeId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogUsers_Milestones_MilestoneId\" TO \"FK_AuditLogWorkSpaces_Milestones_MilestoneId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogUsers_ReportContracts_ReportId\" TO \"FK_AuditLogWorkSpaces_ReportContracts_ReportId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogUsers_Users_UserId\" TO \"FK_AuditLogWorkSpaces_Users_UserId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogWorkSpaces_Users_UserId\" TO \"FK_AuditLogUsers_Users_UserId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogWorkSpaces_ReportContracts_ReportId\" TO \"FK_AuditLogUsers_ReportContracts_ReportId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogWorkSpaces_Milestones_MilestoneId\" TO \"FK_AuditLogUsers_Milestones_MilestoneId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogWorkSpaces_Disputes_DisputeId\" TO \"FK_AuditLogUsers_Disputes_DisputeId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"FK_AuditLogWorkSpaces_Contracts_ContractId\" TO \"FK_AuditLogUsers_Contracts_ContractId\";");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_UserId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_ReportId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_ReportId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_MilestoneId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_MilestoneId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_DisputeId_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_DisputeId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_DisputeId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_DisputeId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_ContractId_CreatedAt",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_ContractId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogWorkSpaces_ContractId",
                table: "AuditLogWorkSpaces",
                newName: "IX_AuditLogUsers_ContractId");

            migrationBuilder.Sql(
                "ALTER TABLE \"AuditLogWorkSpaces\" RENAME CONSTRAINT \"PK_AuditLogWorkSpaces\" TO \"PK_AuditLogUsers\";");

            migrationBuilder.RenameColumn(
                name: "AuditLogWorkSpaceId",
                table: "AuditLogWorkSpaces",
                newName: "AuditLogUsersId");

            migrationBuilder.RenameTable(
                name: "AuditLogWorkSpaces",
                newName: "AuditLogUsers");
        }
    }
}
