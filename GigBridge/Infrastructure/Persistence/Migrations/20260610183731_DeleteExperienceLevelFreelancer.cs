using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeleteExperienceLevelFreelancer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FreelancerProfiles_ExperienceLevel",
                table: "FreelancerProfiles");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "FreelancerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperienceLevel",
                table: "FreelancerProfiles",
                type: "integer",
                nullable: true,
                comment: "Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_ExperienceLevel",
                table: "FreelancerProfiles",
                column: "ExperienceLevel");
        }
    }
}
