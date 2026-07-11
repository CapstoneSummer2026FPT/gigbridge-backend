using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePromotionPackageDependency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransactionId",
                table: "FreelancerProfilePromotions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseIdempotencyKey",
                table: "FreelancerProfilePromotions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "FreelancerProfilePromotions"
                SET "PurchaseIdempotencyKey" = 'legacy:' || "FreelancerProfilePromotionsId"::text
                WHERE "PurchaseIdempotencyKey" = '';

                UPDATE "PlatformSettings"
                SET "Value" = ("Value"::jsonb ||
                    '{"defaultDurationDays":7,"maxQueuedCampaigns":3}'::jsonb)::text,
                    "UpdatedAt" = NOW()
                WHERE "Key" = 'premium.freelancer.promotion-policy';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfilePromotions_FreelancerProfileId_PurchaseIde~",
                table: "FreelancerProfilePromotions",
                columns: new[] { "FreelancerProfileId", "PurchaseIdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FreelancerProfilePromotions_FreelancerProfileId_PurchaseIde~",
                table: "FreelancerProfilePromotions");

            migrationBuilder.DropColumn(
                name: "PurchaseIdempotencyKey",
                table: "FreelancerProfilePromotions");

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletTransactionId",
                table: "FreelancerProfilePromotions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
