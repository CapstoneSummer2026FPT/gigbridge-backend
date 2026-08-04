using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260728143000_AddPromotionQueuePosition")]
public partial class AddPromotionQueuePosition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "QueuePosition",
            table: "FreelancerProfilePromotions",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.Sql(
            """
            WITH ranked AS (
                SELECT "FreelancerProfilePromotionsId",
                       CAST(ROW_NUMBER() OVER (
                           ORDER BY "BoostWeight" DESC, "CreatedAt", "FreelancerProfilePromotionsId"
                       ) AS integer) AS position
                FROM "FreelancerProfilePromotions"
                WHERE "Status" = 1
                  AND "StartTime" <= now()
                  AND "EndTime" > now()
            )
            UPDATE "FreelancerProfilePromotions" AS promotion
            SET "QueuePosition" = ranked.position
            FROM ranked
            WHERE promotion."FreelancerProfilePromotionsId" =
                  ranked."FreelancerProfilePromotionsId";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_FreelancerProfilePromotions_Position",
            table: "FreelancerProfilePromotions",
            columns: new[] { "Status", "QueuePosition" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FreelancerProfilePromotions_Position",
            table: "FreelancerProfilePromotions");

        migrationBuilder.DropColumn(
            name: "QueuePosition",
            table: "FreelancerProfilePromotions");
    }
}
