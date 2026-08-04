using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenshotCheatingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FocusLossCount",
                table: "FreelancerCheatingViolations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FullscreenExitCount",
                table: "FreelancerCheatingViolations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotAttemptCount",
                table: "FreelancerCheatingViolations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FocusLossCount",
                table: "FreelancerCheatingViolations");

            migrationBuilder.DropColumn(
                name: "FullscreenExitCount",
                table: "FreelancerCheatingViolations");

            migrationBuilder.DropColumn(
                name: "ScreenshotAttemptCount",
                table: "FreelancerCheatingViolations");
        }
    }
}
