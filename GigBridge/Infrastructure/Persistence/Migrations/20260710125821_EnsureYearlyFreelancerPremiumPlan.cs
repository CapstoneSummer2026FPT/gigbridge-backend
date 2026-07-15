using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureYearlyFreelancerPremiumPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SubscriptionPlans"
                SET "Price" = CEIL("Price" / 1000.0),
                    "Currency" = 'GigCoin',
                    "UpdatedAt" = NOW()
                WHERE "TargetRole" = 1
                  AND "IsActive" = TRUE
                  AND "Price" > 0
                  AND "DurationInDays" < 360
                  AND UPPER(COALESCE("Currency", '')) = 'VND';

                INSERT INTO "SubscriptionPlans"
                    ("SubscriptionPlansId", "Name", "Description", "Price", "Currency",
                     "DurationInDays", "Features", "TargetRole", "IsActive", "SortOrder", "CreatedAt")
                SELECT
                    '95000000-0000-0000-0000-000000000003',
                    'Freelancer Premium Yearly',
                    'A full year of Freelancer Premium with two months free',
                    monthly."Price" * 10,
                    'GigCoin',
                    365,
                    monthly."Features",
                    1,
                    TRUE,
                    COALESCE(monthly."SortOrder", 0) + 1,
                    NOW()
                FROM "SubscriptionPlans" monthly
                WHERE monthly."TargetRole" = 1
                  AND monthly."IsActive" = TRUE
                  AND monthly."Price" > 0
                  AND monthly."DurationInDays" < 360
                  AND NOT EXISTS (
                      SELECT 1 FROM "SubscriptionPlans" yearly
                      WHERE yearly."TargetRole" = 1
                        AND yearly."IsActive" = TRUE
                        AND yearly."Price" > 0
                        AND yearly."DurationInDays" >= 360)
                ORDER BY monthly."SortOrder", monthly."Price"
                LIMIT 1
                ON CONFLICT ("SubscriptionPlansId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SubscriptionPlans"
                WHERE "SubscriptionPlansId" = '95000000-0000-0000-0000-000000000003';
                """);
        }
    }
}
