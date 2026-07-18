using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260718090000_AddJobPromotionCards")]
public sealed class AddJobPromotionCards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ClickCount",
            table: "JobPostPromotions",
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "JobPostPromotions",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<int>(
            name: "ImpressionCount",
            table: "JobPostPromotions",
            type: "integer",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "PromotionDescription",
            table: "JobPostPromotions",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: false,
            defaultValue: "");
        migrationBuilder.AddColumn<string>(
            name: "PromotionTitle",
            table: "JobPostPromotions",
            type: "character varying(140)",
            maxLength: 140,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ClickCount", table: "JobPostPromotions");
        migrationBuilder.DropColumn(name: "ImageUrl", table: "JobPostPromotions");
        migrationBuilder.DropColumn(name: "ImpressionCount", table: "JobPostPromotions");
        migrationBuilder.DropColumn(name: "PromotionDescription", table: "JobPostPromotions");
        migrationBuilder.DropColumn(name: "PromotionTitle", table: "JobPostPromotions");
    }
}
