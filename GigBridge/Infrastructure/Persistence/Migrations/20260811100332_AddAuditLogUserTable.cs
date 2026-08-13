using Domain.Enums.Accounts;
using Domain.Enums.Auditing;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogUsers",
                columns: table => new
                {
                    AuditLogUsersId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserRole = table.Column<int>(type: "integer", nullable: false, comment: "Enum UserRole: 0=Client, 1=Freelancer, 2=Admin"),
                    ActionType = table.Column<int>(type: "integer", nullable: false, comment: "Enum AuditUserActionType: 0=ConfirmedParticipation, 1=SignedEsignContract, 2=RequestedEarlyStart, 3=MilestoneSubmitted, 4=EscrowFunded, 5=MilestoneApproved, 6=ReportCreated, 7=DisputeCreated, 8=DisputeEscalated"),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostId = table.Column<Guid>(type: "uuid", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisputeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogUsers", x => x.AuditLogUsersId);
                    table.ForeignKey(
                        name: "FK_AuditLogUsers_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogUsers_Disputes_DisputeId",
                        column: x => x.DisputeId,
                        principalTable: "Disputes",
                        principalColumn: "DisputesId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogUsers_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogUsers_ReportContracts_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ReportContracts",
                        principalColumn: "ReportContractId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditLogUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_ContractId",
                table: "AuditLogUsers",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_ContractId_CreatedAt",
                table: "AuditLogUsers",
                columns: new[] { "ContractId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_CreatedAt",
                table: "AuditLogUsers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_DisputeId",
                table: "AuditLogUsers",
                column: "DisputeId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_DisputeId_CreatedAt",
                table: "AuditLogUsers",
                columns: new[] { "DisputeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_MilestoneId",
                table: "AuditLogUsers",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_ReportId",
                table: "AuditLogUsers",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogUsers_UserId",
                table: "AuditLogUsers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogUsers");
        }
    }
}
