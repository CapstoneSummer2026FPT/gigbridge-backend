using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFreelancerProfileTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "FreelancerProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FreelancerProfileCategories",
                columns: table => new
                {
                    FreelancerProfileCategoriesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MajorCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerProfileCategories_pkey", x => x.FreelancerProfileCategoriesId);
                    table.ForeignKey(
                        name: "FreelancerProfileCategories_majorCategory_MajorCategoryId_fkey",
                        column: x => x.MajorCategoryId,
                        principalTable: "MajorCategories",
                        principalColumn: "MajorCategoriesId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FreelancerProfileCategories_profile_FreelancerProfileId_fkey",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "FreelancerProfilesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_MajorId",
                table: "FreelancerProfiles",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "FreelancerProfileCategories_Profile_MajorCategory_key",
                table: "FreelancerProfileCategories",
                columns: new[] { "FreelancerProfileId", "MajorCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfileCategories_FreelancerProfileId",
                table: "FreelancerProfileCategories",
                column: "FreelancerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfileCategories_MajorCategoryId",
                table: "FreelancerProfileCategories",
                column: "MajorCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FreelancerProfiles_major_MajorId_fkey",
                table: "FreelancerProfiles",
                column: "MajorId",
                principalTable: "Majors",
                principalColumn: "MajorsId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FreelancerProfiles_major_MajorId_fkey",
                table: "FreelancerProfiles");

            migrationBuilder.DropTable(
                name: "FreelancerProfileCategories");

            migrationBuilder.DropIndex(
                name: "IX_FreelancerProfiles_MajorId",
                table: "FreelancerProfiles");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "FreelancerProfiles");
        }
    }
}
