using Domain.Enums.Reports;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportContracts",
                columns: table => new
                {
                    ReportContractId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespondentId = table.Column<Guid>(type: "uuid", nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssueType = table.Column<int>(type: "integer", nullable: false, comment: "Enum ContractReportIssueType: 0=PaymentIssue, 1=MilestoneIssue, 2=Delay, 3=PoorQuality, 4=CommunicationProblem, 5=ScopeChange, 6=Other"),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    DesiredResolution = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum ContractReportStatus: 0=Pending, 1=WaitingReporterConfirmation, 2=Resolved, 3=Escalated"),
                    ResolutionAction = table.Column<int>(type: "integer", nullable: true, comment: "Enum ContractReportResolutionAction: 0=AcceptIssue, 1=ProvideExplanation, 2=ProposeResolution, 3=RejectIssue"),
                    Explanation = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ProposedResolution = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEscalatedToDispute = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ReportContracts_pkey", x => x.ReportContractId);
                    table.ForeignKey(
                        name: "ReportContracts_cont_ContractId_fkey",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ContractsId");
                    table.ForeignKey(
                        name: "ReportContracts_mStone_MilestoneId_fkey",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "MilestonesId");
                    table.ForeignKey(
                        name: "ReportContracts_usr_ReporterId_fkey",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "ReportContracts_usr_ResolvedBy_fkey",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "ReportContracts_usr_RespondentId_fkey",
                        column: x => x.RespondentId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ReportContractAttachments",
                columns: table => new
                {
                    ReportContractAttachmentId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ReportContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ReportContractAttachments_pkey", x => x.ReportContractAttachmentId);
                    table.ForeignKey(
                        name: "ReportContractAttachments_rc_ReportContractId_fkey",
                        column: x => x.ReportContractId,
                        principalTable: "ReportContracts",
                        principalColumn: "ReportContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportContractAttachments_ReportContractId",
                table: "ReportContractAttachments",
                column: "ReportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_ContractId",
                table: "ReportContracts",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_MilestoneId",
                table: "ReportContracts",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_ReporterId",
                table: "ReportContracts",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_ResolvedBy",
                table: "ReportContracts",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_RespondentId",
                table: "ReportContracts",
                column: "RespondentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportContracts_Status",
                table: "ReportContracts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportContractAttachments");

            migrationBuilder.DropTable(
                name: "ReportContracts");
        }
    }
}
