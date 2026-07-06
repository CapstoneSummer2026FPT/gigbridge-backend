using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ProposalCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisSummary",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Assumptions",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Deliverables",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutOfScope",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolutionApproach",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProposalMilestonePlans",
                columns: table => new
                {
                    ProposalMilestonePlansId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "text", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalMilestonePlans_pkey", x => x.ProposalMilestonePlansId);
                    table.ForeignKey(
                        name: "ProposalMilestonePlans_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposalWorkBreakdownItems",
                columns: table => new
                {
                    ProposalWorkBreakdownItemsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deliverables = table.Column<string>(type: "text", nullable: true),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalWorkBreakdownItems_pkey", x => x.ProposalWorkBreakdownItemsId);
                    table.ForeignKey(
                        name: "ProposalWorkBreakdownItems_ProposalsId_fkey",
                        column: x => x.ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMilestonePlans_Proposal_Order",
                table: "ProposalMilestonePlans",
                columns: new[] { "ProposalsId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalWorkBreakdownItems_Proposal_Order",
                table: "ProposalWorkBreakdownItems",
                columns: new[] { "ProposalsId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalMilestonePlans");

            migrationBuilder.DropTable(
                name: "ProposalWorkBreakdownItems");

            migrationBuilder.DropColumn(
                name: "AnalysisSummary",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Assumptions",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "Deliverables",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "OutOfScope",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "SolutionApproach",
                table: "Proposals");
        }
    }
}
