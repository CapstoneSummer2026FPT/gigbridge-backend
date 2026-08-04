using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260722040000_ReplaceTalentMatchLlmWithAlgorithmScoring")]
public partial class ReplaceTalentMatchLlmWithAlgorithmScoring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "LlmScore",
            table: "TalentMatchResults",
            newName: "AlgorithmScore");

        migrationBuilder.RenameColumn(
            name: "PromptVersion",
            table: "TalentMatchRuns",
            newName: "ScoringVersion");

        migrationBuilder.DropColumn(
            name: "LlmModel",
            table: "TalentMatchRuns");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LlmModel",
            table: "TalentMatchRuns",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.RenameColumn(
            name: "ScoringVersion",
            table: "TalentMatchRuns",
            newName: "PromptVersion");

        migrationBuilder.RenameColumn(
            name: "AlgorithmScore",
            table: "TalentMatchResults",
            newName: "LlmScore");
    }
}
