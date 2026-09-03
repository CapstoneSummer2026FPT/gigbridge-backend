using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalCandidateEvaluationEngineProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TechnicalQualityScore",
                table: "ProposalAiJudgings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ValueScore",
                table: "ProposalAiJudgings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerdictBadge",
                table: "ProposalAiJudgings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityBand",
                table: "ProposalAiJudgings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SavingsRatioPercent",
                table: "ProposalAiJudgings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ScopeCompletenessPercent",
                table: "ProposalAiJudgings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullEvaluationJson",
                table: "ProposalAiJudgings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TechnicalQualityScore",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "ValueScore",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "VerdictBadge",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "QualityBand",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "SavingsRatioPercent",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "ScopeCompletenessPercent",
                table: "ProposalAiJudgings");

            migrationBuilder.DropColumn(
                name: "FullEvaluationJson",
                table: "ProposalAiJudgings");
        }
    }
}
