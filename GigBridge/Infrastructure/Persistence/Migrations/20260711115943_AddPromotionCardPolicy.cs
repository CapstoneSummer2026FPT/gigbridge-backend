using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionCardPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "FreelancerProfilePromotions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "FreelancerProfilePromotions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "FreelancerProfilePromotions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Quote",
                table: "FreelancerProfilePromotions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowJobTitle",
                table: "FreelancerProfilePromotions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowQuote",
                table: "FreelancerProfilePromotions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TargetClickCount",
                table: "FreelancerProfilePromotions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "FreelancerProfilePromotions" AS promotion
                SET "DisplayName" = COALESCE(NULLIF(TRIM(app_user."FullName"), ''), 'Freelancer'),
                    "PhotoUrl" = COALESCE(app_user."Avatar", ''),
                    "JobTitle" = profile."Title",
                    "ShowJobTitle" = profile."Title" IS NOT NULL AND TRIM(profile."Title") <> '',
                    "TargetClickCount" = 40 + CAST(promotion."TokenCost" * 10 AS integer),
                    "BoostWeight" = promotion."TokenCost"
                FROM "FreelancerProfiles" AS profile
                INNER JOIN "Users" AS app_user ON app_user."UserId" = profile."UserId"
                WHERE profile."FreelancerProfilesId" = promotion."FreelancerProfileId";

                INSERT INTO "PlatformSettings"
                    ("PlatformSettingsId", "Key", "Value", "Description", "DataType", "UpdatedAt")
                VALUES
                    (gen_random_uuid(), 'premium.freelancer.promotion-policy',
                     '{"baseTargetClicks":40,"targetClicksPerCoin":10,"boostWeightPerCoin":1,"minimumBoostCoins":1,"maximumBoostCoinsPerTransaction":1000,"displayNameMaxLength":120,"quoteMaxLength":240,"jobTitleMaxLength":160,"photoUrlMaxLength":2048,"maximumPhotoBytes":5242880,"visitorKeyMaxLength":128,"defaultFeedLimit":12,"maximumFeedLimit":50,"interactionDeduplicationSeconds":60}',
                     'Promotion card, boost, and interaction policy.', 'json', NOW())
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"PlatformSettings\" WHERE \"Key\" = 'premium.freelancer.promotion-policy';");
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "Quote",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "ShowJobTitle",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "ShowQuote",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "TargetClickCount",
                table: "FreelancerProfilePromotions");
        }
    }
}
