using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureFreelancerPremiumGigCoinPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SubscriptionPlans"
                SET "Name" = 'Freelancer Premium Monthly',
                    "Description" = 'Premium benefits billed monthly with GigCoin',
                    "Price" = 150.0,
                    "Currency" = 'GigCoin',
                    "DurationInDays" = 30,
                    "Features" = '["Unlimited proposals", "Premium identity decoration", "Elo tiers and rank protection", "Profile promotion access"]',
                    "TargetRole" = 1,
                    "IsActive" = TRUE,
                    "SortOrder" = 1,
                    "UpdatedAt" = NOW()
                WHERE "SubscriptionPlansId" = 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02';

                INSERT INTO "SubscriptionPlans"
                    ("SubscriptionPlansId", "Name", "Description", "Price", "Currency", "DurationInDays", "Features", "TargetRole", "IsActive", "SortOrder", "CreatedAt")
                VALUES
                    ('e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b05', 'Freelancer Premium Yearly',
                     'Premium benefits for a full year with two months saved', 1500.0, 'GigCoin', 365,
                     '["Unlimited proposals", "Premium identity decoration", "Elo tiers and rank protection", "Profile promotion access", "Save 300 GigCoin per year"]',
                     1, TRUE, 2, NOW())
                ON CONFLICT ("SubscriptionPlansId") DO UPDATE SET
                    "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description",
                    "Price" = EXCLUDED."Price", "Currency" = EXCLUDED."Currency",
                    "DurationInDays" = EXCLUDED."DurationInDays", "Features" = EXCLUDED."Features",
                    "TargetRole" = EXCLUDED."TargetRole", "IsActive" = EXCLUDED."IsActive",
                    "SortOrder" = EXCLUDED."SortOrder", "UpdatedAt" = NOW();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SubscriptionPlans"
                WHERE "SubscriptionPlansId" = 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b05';

                UPDATE "SubscriptionPlans"
                SET "Name" = 'Freelancer Pro', "Description" = 'Pro plan for advanced freelancers with premium features',
                    "Price" = 150000.0, "Currency" = 'VND', "DurationInDays" = 30,
                    "SortOrder" = 0, "UpdatedAt" = NOW()
                WHERE "SubscriptionPlansId" = 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b02';
                """);
        }
    }
}
