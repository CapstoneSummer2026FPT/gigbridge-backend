using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260729143000_EnforceUniquePromotionQueuePosition")]
public partial class EnforceUniquePromotionQueuePosition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FreelancerProfilePromotions_Position",
            table: "FreelancerProfilePromotions");

        migrationBuilder.Sql(
            """
            UPDATE "FreelancerProfilePromotions"
            SET "QueuePosition" = 0
            WHERE "QueuePosition" <> 0;

            WITH ranked AS (
                SELECT "FreelancerProfilePromotionsId",
                       CAST(ROW_NUMBER() OVER (
                           ORDER BY "BoostWeight" DESC, "CreatedAt",
                                    "FreelancerProfilePromotionsId"
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
            columns: new[] { "Status", "QueuePosition" },
            unique: true,
            filter: "\"QueuePosition\" > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FreelancerProfilePromotions_Position",
            table: "FreelancerProfilePromotions");

        migrationBuilder.CreateIndex(
            name: "IX_FreelancerProfilePromotions_Position",
            table: "FreelancerProfilePromotions",
            columns: new[] { "Status", "QueuePosition" });
    }
}
