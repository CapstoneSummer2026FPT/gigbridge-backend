using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Repairs environments where AddJobPromotionCards was recorded in migration history
/// before all of its columns were present. Every statement is idempotent.
/// </summary>
[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260718113000_RepairJobPromotionCardColumns")]
public sealed class RepairJobPromotionCardColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "JobPostPromotions"
                ADD COLUMN IF NOT EXISTS "ClickCount" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "ImageUrl" character varying(2048) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "ImpressionCount" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "PromotionDescription" character varying(1000) NOT NULL DEFAULT '',
                ADD COLUMN IF NOT EXISTS "PromotionTitle" character varying(140) NOT NULL DEFAULT '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: these columns are owned by AddJobPromotionCards.
        // Rolling back this repair must not remove schema required by that migration.
    }
}
