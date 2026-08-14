using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260814170000_AddFreelancerSearchEngineVisibility")]
public partial class AddFreelancerSearchEngineVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AllowSearchEngineIndexing",
            table: "FreelancerProfiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_FreelancerProfiles_AllowSearchEngineIndexing",
            table: "FreelancerProfiles",
            column: "AllowSearchEngineIndexing");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FreelancerProfiles_AllowSearchEngineIndexing",
            table: "FreelancerProfiles");

        migrationBuilder.DropColumn(
            name: "AllowSearchEngineIndexing",
            table: "FreelancerProfiles");
    }
}
