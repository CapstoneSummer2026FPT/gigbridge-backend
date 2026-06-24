using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleContractPerJobPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "MaxHires",
                table: "JobPosts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts",
                column: "JobPostsId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts");

            migrationBuilder.AddColumn<int>(
                name: "MaxHires",
                table: "JobPosts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts",
                column: "JobPostsId");
        }
    }
}
