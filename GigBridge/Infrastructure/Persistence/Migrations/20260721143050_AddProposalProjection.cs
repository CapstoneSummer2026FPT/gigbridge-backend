using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalAiJudgings",
                columns: table => new
                {
                    ProposalAiJudgingsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    RecommendedHire = table.Column<bool>(type: "boolean", nullable: false),
                    TechnicalSkillsJson = table.Column<string>(type: "text", nullable: true),
                    SoftSkillsJson = table.Column<string>(type: "text", nullable: true),
                    HolisticAdjustment = table.Column<int>(type: "integer", nullable: false),
                    HolisticAdjustmentReason = table.Column<string>(type: "text", nullable: true),
                    GradedQuestionsJson = table.Column<string>(type: "text", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalAiJudgings_pkey", x => x.ProposalAiJudgingsId);
                    table.ForeignKey(
                        name: "ProposalAiJudgings_ProposalId_fkey",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "ProposalsId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAiJudgings_ProposalId",
                table: "ProposalAiJudgings",
                column: "ProposalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalAiJudgings");
        }
    }
}
