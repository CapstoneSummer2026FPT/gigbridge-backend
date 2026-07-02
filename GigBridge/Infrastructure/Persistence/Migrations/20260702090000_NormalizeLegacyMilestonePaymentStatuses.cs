using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    public partial class NormalizeLegacyMilestonePaymentStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Milestones"
                SET "Status" = 3
                WHERE "Status" IN (4, 5);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data normalization: statuses 4 and 5 are deprecated payment states.
        }
    }
}
