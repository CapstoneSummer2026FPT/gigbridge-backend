using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContractDetailTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "CancellationTerms";
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "ConfidentialityTerms";
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "IntellectualPropertyTerms";
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "PaymentTerms";
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "ScopeOfWork";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfidentialityTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntellectualPropertyTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeOfWork",
                table: "Contracts",
                type: "text",
                nullable: true);
        }
    }
}
