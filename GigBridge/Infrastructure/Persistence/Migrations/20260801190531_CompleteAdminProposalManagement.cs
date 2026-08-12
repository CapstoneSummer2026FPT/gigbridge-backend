using Domain.Enums.Proposals;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAdminProposalManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Proposals",
                type: "integer",
                nullable: false,
                comment: "Enum ProposalStatus: 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvalidatedAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvalidatedByAdminId",
                table: "Proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidationReason",
                table: "Proposals",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationStatus",
                table: "Proposals",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum ProposalModerationStatus: 0=Active, 1=Invalidated");

            migrationBuilder.CreateTable(
                name: "ProposalAdminNotes",
                columns: table => new
                {
                    ProposalAdminNoteId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalAdminNotes", x => x.ProposalAdminNoteId);
                    table.ForeignKey(
                        name: "FK_ProposalAdminNotes_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalAdminNotes_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_InvalidatedByAdminId",
                table: "Proposals",
                column: "InvalidatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ModerationStatus",
                table: "Proposals",
                column: "ModerationStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proposals_ModerationStatus_Valid",
                table: "Proposals",
                sql: "\"ModerationStatus\" BETWEEN 0 AND 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAdminNotes_AdminUserId",
                table: "ProposalAdminNotes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAdminNotes_ProposalId_CreatedAt",
                table: "ProposalAdminNotes",
                columns: new[] { "ProposalId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "Proposals_usr_InvalidatedByAdminId_fkey",
                table: "Proposals",
                column: "InvalidatedByAdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Proposals_usr_InvalidatedByAdminId_fkey",
                table: "Proposals");

            migrationBuilder.DropTable(
                name: "ProposalAdminNotes");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_InvalidatedByAdminId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_ModerationStatus",
                table: "Proposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Proposals_ModerationStatus_Valid",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "InvalidatedAt",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "InvalidatedByAdminId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "InvalidationReason",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "Proposals");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Proposals",
                type: "integer",
                nullable: false,
                comment: "Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Enum ProposalStatus: 0=Draft, 1=Pending, 2=Shortlisted, 3=Accepted, 4=Rejected, 5=Withdrawn");
        }
    }
}
