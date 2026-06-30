using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    public partial class AddMilestoneAttachmentSubmissionSource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "MilestoneAttachments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "MilestoneAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Enum MilestoneSubmissionSourceType: 0=File, 1=Link");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "MilestoneAttachments");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MilestoneAttachments");
        }
    }
}
