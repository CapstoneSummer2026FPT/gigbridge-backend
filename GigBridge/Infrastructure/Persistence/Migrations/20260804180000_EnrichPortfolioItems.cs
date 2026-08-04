using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260804180000_EnrichPortfolioItems")]
public sealed class EnrichPortfolioItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "PortfolioItems",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "PortfolioItems",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "ProjectDate",
            table: "PortfolioItems",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Title",
            table: "PortfolioItems",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Description", table: "PortfolioItems");
        migrationBuilder.DropColumn(name: "ImageUrl", table: "PortfolioItems");
        migrationBuilder.DropColumn(name: "ProjectDate", table: "PortfolioItems");
        migrationBuilder.DropColumn(name: "Title", table: "PortfolioItems");
    }
}
