using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNameviVer2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameVi",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "NameVi",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "NameVi",
                table: "FAQCategories");

            migrationBuilder.DropColumn(
                name: "NameVi",
                table: "ESignTemplates");

            migrationBuilder.DropColumn(
                name: "NameVi",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "SubscriptionPlans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "Skills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "FAQCategories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "ESignTemplates",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameVi",
                table: "Categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
