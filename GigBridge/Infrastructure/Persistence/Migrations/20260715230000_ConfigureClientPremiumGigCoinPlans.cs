using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260715230000_ConfigureClientPremiumGigCoinPlans")]
public sealed class ConfigureClientPremiumGigCoinPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "SubscriptionPlans"
                ("SubscriptionPlansId", "Name", "Description", "Price", "Currency",
                 "DurationInDays", "Features", "TargetRole", "IsActive", "SortOrder", "CreatedAt")
            VALUES
                ('e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04', 'Client Premium Monthly',
                 'Premium hiring tools billed monthly with GigCoin', 500.0, 'GigCoin', 30,
                 '["AI job post generator", "Featured job promotion", "Smart talent matching", "AI interview definitions and results", "VIP dispute fast-track"]',
                 0, TRUE, 1, NOW()),
                ('e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b06', 'Client Premium Yearly',
                 'A full year of Premium hiring tools with two months saved', 5000.0, 'GigCoin', 365,
                 '["AI job post generator", "Featured job promotion", "Smart talent matching", "AI interview definitions and results", "VIP dispute fast-track", "Save 1000 GigCoin per year"]',
                 0, TRUE, 2, NOW())
            ON CONFLICT ("SubscriptionPlansId") DO UPDATE SET
                "Name" = EXCLUDED."Name", "Description" = EXCLUDED."Description",
                "Price" = EXCLUDED."Price", "Currency" = EXCLUDED."Currency",
                "DurationInDays" = EXCLUDED."DurationInDays", "Features" = EXCLUDED."Features",
                "TargetRole" = EXCLUDED."TargetRole", "IsActive" = EXCLUDED."IsActive",
                "SortOrder" = EXCLUDED."SortOrder", "UpdatedAt" = NOW();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "SubscriptionPlans"
            WHERE "SubscriptionPlansId" = 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b06';

            UPDATE "SubscriptionPlans"
            SET "Name" = 'Client Premium',
                "Description" = 'Premium plan for active hiring companies',
                "Price" = 500000.0, "Currency" = 'VND', "DurationInDays" = 30,
                "Features" = '["Unlimited job posts", "Advanced AI profile filtering", "Top freelancer recommendations", "24/7 support"]',
                "TargetRole" = 0, "IsActive" = TRUE, "SortOrder" = 0, "UpdatedAt" = NOW()
            WHERE "SubscriptionPlansId" = 'e0eebc99-9c0b-4ef8-bb6d-6bb9bd380b04';
            """);
    }
}
